using System.Globalization;
using ForexZoneAnalyzer.Worker.Configuration;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
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
    private readonly TrendConfiguration _trendConfig;
    private readonly ResiliencePipeline _retryPipeline;

    public ZoneMonitorService(
        CandleCacheService candleCache,
        IZoneStore zoneStore,
        INotificationService notificationService,
        OandaConnectionService connectionService,
        ILogger<ZoneMonitorService> logger,
        IOptions<MonitorSettings> monitorSettings,
        IOptions<ZoneConfiguration> zoneConfig,
        IOptions<TrendConfiguration> trendConfig)
    {
        _candleCache = candleCache;
        _zoneStore = zoneStore;
        _notificationService = notificationService;
        _connectionService = connectionService;
        _logger = logger;
        _monitorSettings = monitorSettings.Value;
        _zoneConfig = zoneConfig.Value;
        _trendConfig = trendConfig.Value;

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(5),
                OnRetry = args =>
                {
                    _logger.LogWarning("Retry attempt {Attempt} after {Delay}s due to: {Error}",
                        args.AttemptNumber + 1, args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.Message ?? "unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var instruments = _monitorSettings.Instruments
            .Select(i => Enum.Parse<InstrumentName>(i, ignoreCase: true))
            .ToArray();

        // Parse all timeframe pairs, deduplicating by zone granularity
        var timeframePairs = _monitorSettings.Timeframes
            .GroupBy(tf => tf.ZoneGranularity, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(tf => (
                Zone: Enum.Parse<CandlestickGranularity>(tf.ZoneGranularity, ignoreCase: true),
                Trend: Enum.Parse<CandlestickGranularity>(tf.TrendGranularity, ignoreCase: true),
                ZoneStr: tf.ZoneGranularity,
                TrendStr: tf.TrendGranularity
            )).ToArray();

        _logger.LogInformation("Loaded {Count} timeframe pairs from config (raw config had {RawCount} entries)",
            timeframePairs.Length, _monitorSettings.Timeframes.Length);

        // Poll interval is the smallest zone granularity
        var intervalMinutes = timeframePairs.Min(tf => GetGranularityMinutes(tf.Zone));
        var timeframeList = string.Join(", ", timeframePairs.Select(tf => $"{tf.ZoneStr}→{tf.TrendStr}"));
        _logger.LogInformation("Zone Monitor starting. Instruments: [{Instruments}], Timeframes: [{Timeframes}], Interval: {Interval}min",
            string.Join(", ", _monitorSettings.Instruments),
            timeframeList,
            intervalMinutes);

        // Wait for the first candle-aligned slot before starting
        var initialDelay = GetDelayUntilNextSlot(DateTime.UtcNow, intervalMinutes);
        _logger.LogInformation("Waiting {Delay} until first candle-aligned slot at {NextRun:HH:mm:ss} UTC",
            initialDelay, DateTime.UtcNow + initialDelay);
        await Task.Delay(initialDelay, stoppingToken);

        var firstCycle = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            var cycleStart = DateTime.UtcNow;

            try
            {
                // On first cycle, check storage freshness to skip timeframes
                // that were already processed recently (e.g. after a restart).
                // On subsequent cycles, use candle-alignment gating.
                var dueTimeframes = firstCycle
                    ? await GetStaleTimeframesAsync(instruments[0], timeframePairs, cycleStart, intervalMinutes, stoppingToken)
                    : timeframePairs.Where(tf =>
                        ShouldProcessTimeframe(cycleStart, GetGranularityMinutes(tf.Zone), intervalMinutes)
                      ).ToArray();

                if (dueTimeframes.Length > 0)
                {
                    var tfNames = string.Join(", ", dueTimeframes.Select(tf => tf.ZoneStr));
                    _logger.LogInformation("Processing {Count} timeframes this cycle: [{Timeframes}]",
                        dueTimeframes.Length, tfNames);

                    var tasks = instruments.Select(instrument =>
                        ProcessInstrumentAllTimeframesAsync(instrument, dueTimeframes, stoppingToken)
                    ).ToArray();

                    await Task.WhenAll(tasks);
                }
                else
                {
                    _logger.LogDebug("No timeframes due this cycle");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during zone monitoring cycle");
            }

            var delay = GetDelayUntilNextSlot(DateTime.UtcNow, intervalMinutes);
            _logger.LogInformation("Cycle complete. Next poll at {NextRun:HH:mm:ss} UTC (in {Delay})",
                DateTime.UtcNow + delay, delay);
            firstCycle = false;
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("Zone Monitor stopping.");
    }

    /// <summary>
    /// Process all due timeframes for a single instrument. Each instrument runs
    /// in parallel with others; timeframes within an instrument run sequentially.
    /// </summary>
    private async Task ProcessInstrumentAllTimeframesAsync(
        InstrumentName instrument,
        (CandlestickGranularity Zone, CandlestickGranularity Trend, string ZoneStr, string TrendStr)[] timeframes,
        CancellationToken cancellationToken)
    {
        foreach (var tf in timeframes)
        {
            try
            {
                await _retryPipeline.ExecuteAsync(
                    async ct => await ProcessInstrumentAsync(instrument, tf.Zone, tf.Trend, tf.ZoneStr, ct),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to process {Instrument} {Granularity} after retries",
                    instrument, tf.ZoneStr);
            }
        }
    }

    /// <summary>
    /// Checks storage freshness for each timeframe and returns only those
    /// that are stale (no stored data, or last update is older than the candle interval).
    /// Used on first cycle after restart to avoid redundant OANDA calls.
    /// </summary>
    private async Task<(CandlestickGranularity Zone, CandlestickGranularity Trend, string ZoneStr, string TrendStr)[]>
        GetStaleTimeframesAsync(
            InstrumentName sampleInstrument,
            (CandlestickGranularity Zone, CandlestickGranularity Trend, string ZoneStr, string TrendStr)[] allTimeframes,
            DateTime utcNow,
            int pollIntervalMinutes,
            CancellationToken cancellationToken)
    {
        var stale = new List<(CandlestickGranularity Zone, CandlestickGranularity Trend, string ZoneStr, string TrendStr)>();

        foreach (var tf in allTimeframes)
        {
            var lastUpdated = await _zoneStore.GetLastUpdatedAsync(
                sampleInstrument.ToString(), tf.ZoneStr, cancellationToken);

            var candleMinutes = GetGranularityMinutes(tf.Zone);

            var isStale = lastUpdated == null || (utcNow - lastUpdated.Value).TotalMinutes >= candleMinutes;
            var isCandleAligned = ShouldProcessTimeframe(utcNow, candleMinutes, pollIntervalMinutes);

            if (isStale && isCandleAligned)
            {
                stale.Add(tf);
                _logger.LogDebug("Timeframe {TF} is stale and candle-aligned (last updated: {LastUpdated})", tf.ZoneStr,
                    lastUpdated?.ToString("yyyy-MM-dd HH:mm") ?? "never");
            }
            else if (isStale)
            {
                _logger.LogDebug("Timeframe {TF} is stale but not candle-aligned — deferring to next candle close", tf.ZoneStr);
            }
            else
            {
                _logger.LogInformation("Skipping {TF} on restart — still fresh (updated {Ago:N0} min ago)",
                    tf.ZoneStr, (utcNow - lastUpdated.Value).TotalMinutes);
            }
        }

        return stale.ToArray();
    }
    /// Determines whether a timeframe should be processed in the current cycle.
    /// A timeframe is due when the current time aligns with its candle interval.
    /// </summary>
    internal static bool ShouldProcessTimeframe(DateTime utcNow, int timeframeMinutes, int pollIntervalMinutes)
    {
        var totalMinutes = utcNow.Hour * 60 + utcNow.Minute;
        // The candle closed at the start of the current slot for this timeframe
        return totalMinutes % timeframeMinutes < pollIntervalMinutes;
    }

    /// <summary>
    /// Calculates delay until 1 minute after the next candle close, based on the
    /// configured granularity interval. E.g., M15 → :01/:16/:31/:46, H1 → :01 each hour.
    /// </summary>
    internal static TimeSpan GetDelayUntilNextSlot(DateTime utcNow, int intervalMinutes)
    {
        const int offsetMinutes = 1;

        // Total minutes since midnight
        var totalMinutes = utcNow.Hour * 60 + utcNow.Minute;

        // Floor to start of current slot
        var slotStartMinutes = totalMinutes / intervalMinutes * intervalMinutes;
        var slotStart = utcNow.Date.AddMinutes(slotStartMinutes);

        // Target is slotStart + offset
        var target = slotStart.AddMinutes(offsetMinutes);

        // If we're already past the target in this slot, move to next slot
        if (utcNow >= target)
            target = target.AddMinutes(intervalMinutes);

        return target - utcNow;
    }

    /// <summary>
    /// Maps a CandlestickGranularity to its duration in minutes.
    /// </summary>
    internal static int GetGranularityMinutes(CandlestickGranularity granularity) => granularity switch
    {
        CandlestickGranularity.S5 => 1,    // sub-minute → poll every minute
        CandlestickGranularity.S10 => 1,
        CandlestickGranularity.S15 => 1,
        CandlestickGranularity.S30 => 1,
        CandlestickGranularity.M1 => 1,
        CandlestickGranularity.M2 => 2,
        CandlestickGranularity.M4 => 4,
        CandlestickGranularity.M5 => 5,
        CandlestickGranularity.M10 => 10,
        CandlestickGranularity.M15 => 15,
        CandlestickGranularity.M30 => 30,
        CandlestickGranularity.H1 => 60,
        CandlestickGranularity.H2 => 120,
        CandlestickGranularity.H3 => 180,
        CandlestickGranularity.H4 => 240,
        CandlestickGranularity.H6 => 360,
        CandlestickGranularity.H8 => 480,
        CandlestickGranularity.H12 => 720,
        CandlestickGranularity.D => 1440,
        _ => 15 // fallback
    };

    private async Task ProcessInstrumentAsync(
        InstrumentName instrument,
        CandlestickGranularity zoneGranularity,
        CandlestickGranularity trendGranularity,
        string granularityStr,
        CancellationToken cancellationToken)
    {
        var instrumentName = instrument.ToString();
        _logger.LogDebug("Processing {Instrument} {Granularity}", instrumentName, granularityStr);

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
        var persistedZones = await _zoneStore.GetZonesAsync(instrumentName, granularityStr, cancellationToken);

        // 4. Find new zones (not in persisted set)
        var persistedKeys = new HashSet<string>(persistedZones.Select(GetZoneKey));
        var newZones = freshZones.Where(z => !persistedKeys.Contains(GetZoneKey(z))).ToList();

        // 5. Persist all fresh zones (updates Freshness, Worked, SubZone for existing ones)
        await _zoneStore.UpsertZonesAsync(instrumentName, granularityStr, freshZones, cancellationToken);

        // 6. Compute and persist trend
        var trend = await GetTrendAsync(instrument, trendGranularity, cancellationToken);
        await _zoneStore.UpsertTrendAsync(instrumentName, granularityStr, trend, cancellationToken);

        _logger.LogInformation("{Instrument} {Granularity}: {Total} zones ({New} new, {Supply} supply, {Demand} demand), trend={Trend}",
            instrumentName, granularityStr, freshZones.Count, newZones.Count,
            freshZones.Count(z => z.Type == ZoneType.Supply),
            freshZones.Count(z => z.Type == ZoneType.Demand),
            trend);

        // 7. Send notifications only for H1 timeframe new zones that are not broken
        //    Fire-and-forget: notification failures should not crash or retry the cycle
        if (newZones.Count > 0 && zoneGranularity == CandlestickGranularity.H1)
        {
            var activeNewZones = newZones.Where(z => z.Freshness != ZoneFreshness.Broken).ToList();
            _logger.LogInformation("{Instrument} {Granularity}: {Active} active new zones (skipped {Broken} broken)",
                instrumentName, granularityStr, activeNewZones.Count, newZones.Count - activeNewZones.Count);

            foreach (var zone in activeNewZones)
            {
                try
                {
                    await _notificationService.SendZoneAlertAsync(instrumentName, granularityStr, zone, trend, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification for {Instrument} {Zone}", instrumentName, zone.Type);
                }
            }
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
            var trendManager = TrendManager.Create(trendCandles, _trendConfig);
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
