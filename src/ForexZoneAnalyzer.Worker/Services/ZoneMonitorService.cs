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

            _logger.LogInformation("Cycle complete. Next poll in {Minutes} minutes.", _monitorSettings.PollIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(_monitorSettings.PollIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("Zone Monitor stopping.");
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

            // 6. Send notifications for new zones
            if (newZones.Count > 0)
            {
                var trend = await GetTrendAsync(instrument, trendGranularity, cancellationToken);

                foreach (var zone in newZones)
                {
                    await _notificationService.SendZoneAlertAsync(instrumentName, granularityStr, zone, trend, cancellationToken);
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
