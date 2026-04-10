using System.Globalization;
using ForexZoneAnalyzer.Worker.Configuration;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;
using Microsoft.Extensions.Options;
using ZoneAnalyzer.PatternAnalysis;

namespace ForexZoneAnalyzer.Worker.Services;

public class ZoneMonitorService : BackgroundService
{
    private readonly CandleCacheService _candleCache;
    private readonly IZoneStore _zoneStore;
    private readonly INotificationService _notificationService;
    private readonly OandaConnectionService _connectionService;
    private readonly ILogger<ZoneMonitorService> _logger;
    private readonly MonitorSettings _monitorSettings;
    private readonly ZoneConfiguration _zoneConfig;

    public ZoneMonitorService(
        CandleCacheService candleCache,
        IZoneStore zoneStore,
        INotificationService notificationService,
        OandaConnectionService connectionService,
        ILogger<ZoneMonitorService> logger,
        IOptions<MonitorSettings> monitorSettings,
        IOptions<ZoneConfiguration> zoneConfig)
    {
        _candleCache = candleCache;
        _zoneStore = zoneStore;
        _notificationService = notificationService;
        _connectionService = connectionService;
        _logger = logger;
        _monitorSettings = monitorSettings.Value;
        _zoneConfig = zoneConfig.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Zone Monitor starting. Instruments: [{Instruments}], Zone TF: {ZoneTF}, Trend TF: {TrendTF}, Poll: {Poll}min",
            string.Join(", ", _monitorSettings.Instruments),
            _monitorSettings.ZoneGranularity,
            _monitorSettings.TrendGranularity,
            _monitorSettings.PollIntervalMinutes);

        // Parse granularities once
        var zoneGranularity = Enum.Parse<CandlestickGranularity>(_monitorSettings.ZoneGranularity, ignoreCase: true);
        var trendGranularity = Enum.Parse<CandlestickGranularity>(_monitorSettings.TrendGranularity, ignoreCase: true);
        var instruments = _monitorSettings.Instruments
            .Select(i => Enum.Parse<InstrumentName>(i, ignoreCase: true))
            .ToArray();

        // Wait for the first candle-aligned slot before starting
        var initialDelay = GetDelayUntilNextSlot();
        _logger.LogInformation("Waiting {Delay} until first candle-aligned slot at {NextRun:HH:mm:ss} UTC",
            initialDelay, DateTime.UtcNow + initialDelay);
        await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var instrument in instruments)
                {
                    await ProcessInstrumentAsync(instrument, zoneGranularity, trendGranularity, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during zone monitoring cycle");
            }

            var delay = GetDelayUntilNextSlot();
            _logger.LogInformation("Cycle complete. Next poll at {NextRun:HH:mm:ss} UTC (in {Delay})",
                DateTime.UtcNow + delay, delay);
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("Zone Monitor stopping.");
    }

    /// <summary>
    /// Calculates delay until 1 minute after the next M15 candle close.
    /// M15 candles close at :00, :15, :30, :45 — we run at :01, :16, :31, :46.
    /// </summary>
    internal static TimeSpan GetDelayUntilNextSlot()
    {
        return GetDelayUntilNextSlot(DateTime.UtcNow);
    }

    internal static TimeSpan GetDelayUntilNextSlot(DateTime utcNow)
    {
        const int intervalMinutes = 15;
        const int offsetMinutes = 1;

        // Find the start of the current 15-min slot (floor to :00, :15, :30, :45)
        var slotStart = utcNow.Date.AddHours(utcNow.Hour)
            .AddMinutes(utcNow.Minute / intervalMinutes * intervalMinutes);

        // Target is slotStart + offset (e.g., :01, :16, :31, :46)
        var target = slotStart.AddMinutes(offsetMinutes);

        // If we're already past the target in this slot, move to next slot
        if (utcNow >= target)
            target = target.AddMinutes(intervalMinutes);

        return target - utcNow;
    }

    private async Task ProcessInstrumentAsync(
        InstrumentName instrument,
        CandlestickGranularity zoneGranularity,
        CandlestickGranularity trendGranularity,
        CancellationToken cancellationToken)
    {
        var instrumentName = instrument.ToString();
        _logger.LogDebug("Processing {Instrument}", instrumentName);

        try
        {
            // 1. Get candles from cache (incremental fetch)
            var candles = await _candleCache.GetCandlesAsync(instrument, zoneGranularity, cancellationToken);
            if (candles.Count == 0)
            {
                _logger.LogWarning("No candles for {Instrument} {Granularity}", instrumentName, zoneGranularity);
                return;
            }

            // 2. Run ZoneManager with configurable ZoneConfiguration
            var zoneManager = ZoneManager.Create(candles, _zoneConfig);
            var freshZones = zoneManager.GetSupplyZones().Concat(zoneManager.GetDemandZones()).ToList();

            // 3. Load previously persisted zones
            var granularityStr = _monitorSettings.ZoneGranularity;
            var persistedZones = await _zoneStore.GetZonesAsync(instrumentName, granularityStr, cancellationToken);

            // 4. Find new zones (not in persisted set)
            var persistedKeys = new HashSet<string>(persistedZones.Select(GetZoneKey));
            var newZones = freshZones.Where(z => !persistedKeys.Contains(GetZoneKey(z))).ToList();

            // 5. Persist all fresh zones (updates Freshness, Worked, SubZone for existing ones)
            await _zoneStore.UpsertZonesAsync(instrumentName, granularityStr, freshZones, cancellationToken);

            _logger.LogInformation("{Instrument}: {Total} zones ({New} new, {Supply} supply, {Demand} demand)",
                instrumentName, freshZones.Count, newZones.Count,
                freshZones.Count(z => z.Type == ZoneType.Supply),
                freshZones.Count(z => z.Type == ZoneType.Demand));

            // 6. Send notifications for new zones that are not broken
            if (newZones.Count > 0)
            {
                var activeNewZones = newZones.Where(z => z.Freshness != ZoneFreshness.Broken).ToList();
                _logger.LogInformation("{Instrument}: {Active} active new zones (skipped {Broken} broken)",
                    instrumentName, activeNewZones.Count, newZones.Count - activeNewZones.Count);

                if (activeNewZones.Count > 0)
                {
                    var trend = await GetTrendAsync(instrument, trendGranularity, cancellationToken);

                    foreach (var zone in activeNewZones)
                    {
                        await _notificationService.SendZoneAlertAsync(instrumentName, granularityStr, zone, trend, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {Instrument}", instrumentName);
        }
    }

    private async Task<string> GetTrendAsync(
        InstrumentName instrument,
        CandlestickGranularity trendGranularity,
        CancellationToken cancellationToken)
    {
        try
        {
            var trendCandles = await _candleCache.GetCandlesAsync(instrument, trendGranularity, cancellationToken);
            var trendManager = TrendManager.Create(trendCandles);
            return trendManager.GetTrend();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get trend for {Instrument}", instrument);
            return "Unknown";
        }
    }

    private static string GetZoneKey(Zone zone) =>
        $"{zone.Type}_{zone.StartTime:O}_{zone.BaseRangeHigh}_{zone.BaseRangeLow}";
}
