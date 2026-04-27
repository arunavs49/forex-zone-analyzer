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
    private readonly IConfigStore _configStore;
    private readonly INotificationService _notificationService;
    private readonly OandaConnectionService _connectionService;
    private readonly ILogger<ZoneMonitorService> _logger;
    private readonly MonitorSettings _monitorSettings;
    private readonly ResiliencePipeline _retryPipeline;

    public ZoneMonitorService(
        CandleCacheService candleCache,
        IZoneStore zoneStore,
        IConfigStore configStore,
        INotificationService notificationService,
        OandaConnectionService connectionService,
        ILogger<ZoneMonitorService> logger,
        IOptions<MonitorSettings> monitorSettings)
    {
        _candleCache = candleCache;
        _zoneStore = zoneStore;
        _configStore = configStore;
        _notificationService = notificationService;
        _connectionService = connectionService;
        _logger = logger;
        _monitorSettings = monitorSettings.Value;

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
        // Use a fixed poll interval from MonitorSettings (smallest configured TF, or default 5 min)
        var intervalMinutes = _monitorSettings.Timeframes.Length > 0
            ? _monitorSettings.Timeframes
                .Select(tf => GetGranularityMinutes(
                    Enum.Parse<CandlestickGranularity>(tf.ZoneGranularity, ignoreCase: true)))
                .Min()
            : 5;

        _logger.LogInformation("Zone Monitor starting. Poll interval: {Interval}min. Configs from Table Storage.",
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
                // Load enabled configs from store each cycle
                var enabledConfigs = await _configStore.GetEnabledConfigsAsync(stoppingToken);

                if (enabledConfigs.Count == 0)
                {
                    _logger.LogDebug("No enabled pair configs found");
                }
                else
                {
                    // Group configs by instrument for parallel processing
                    var byInstrument = enabledConfigs.GroupBy(c => c.Instrument);

                    // On first cycle, filter to stale configs only
                    var configsToProcess = firstCycle
                        ? await GetStaleConfigsAsync(enabledConfigs, cycleStart, stoppingToken)
                        : GetDueConfigs(enabledConfigs, cycleStart, intervalMinutes);

                    if (configsToProcess.Count > 0)
                    {
                        var configNames = string.Join(", ", configsToProcess.Select(c => $"{c.Instrument}/{c.ZoneGranularity}"));
                        _logger.LogInformation("Processing {Count} configs this cycle: [{Configs}]",
                            configsToProcess.Count, configNames);

                        // Process each instrument in parallel, configs within sequentially
                        var tasks = configsToProcess
                            .GroupBy(c => c.Instrument)
                            .Select(group => ProcessInstrumentConfigsAsync(group.Key, group.ToList(), stoppingToken))
                            .ToArray();

                        await Task.WhenAll(tasks);
                    }
                    else
                    {
                        _logger.LogDebug("No configs due this cycle");
                    }
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
    /// Process all configs for a single instrument sequentially.
    /// </summary>
    private async Task ProcessInstrumentConfigsAsync(
        string instrument,
        List<PairConfig> configs,
        CancellationToken cancellationToken)
    {
        foreach (var config in configs)
        {
            try
            {
                await _retryPipeline.ExecuteAsync(
                    async ct => await ProcessConfigAsync(config, ct),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to process {Instrument} {Granularity} after retries",
                    instrument, config.ZoneGranularity);
            }
        }
    }

    /// <summary>
    /// On first cycle, return only configs whose stored data is stale.
    /// </summary>
    private async Task<List<PairConfig>> GetStaleConfigsAsync(
        List<PairConfig> configs,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var stale = new List<PairConfig>();

        foreach (var config in configs)
        {
            var status = await _configStore.GetStatusAsync(config.Instrument, config.ZoneGranularity, cancellationToken);
            var candleMinutes = GetGranularityMinutes(
                Enum.Parse<CandlestickGranularity>(config.ZoneGranularity, ignoreCase: true));

            // Stale if: never processed, config version changed, or data is older than candle interval
            if (status == null
                || status.ConfigVersionProcessed != config.ConfigVersion
                || !status.LastProcessedUtc.HasValue
                || (utcNow - status.LastProcessedUtc.Value).TotalMinutes >= candleMinutes)
            {
                stale.Add(config);
                _logger.LogDebug("{Instrument} {TF} is stale (status: {Status})",
                    config.Instrument, config.ZoneGranularity,
                    status == null ? "never processed" : $"v{status.ConfigVersionProcessed}, last={status.LastProcessedUtc:HH:mm}");
            }
            else
            {
                _logger.LogInformation("Skipping {Instrument} {TF} on restart — still fresh (updated {Ago:N0} min ago)",
                    config.Instrument, config.ZoneGranularity,
                    (utcNow - status.LastProcessedUtc.Value).TotalMinutes);
            }
        }

        return stale;
    }

    /// <summary>
    /// Return configs whose zone timeframe is due based on candle alignment.
    /// </summary>
    private static List<PairConfig> GetDueConfigs(List<PairConfig> configs, DateTime utcNow, int pollIntervalMinutes)
    {
        return configs.Where(config =>
        {
            var tfMinutes = GetGranularityMinutes(
                Enum.Parse<CandlestickGranularity>(config.ZoneGranularity, ignoreCase: true));
            return ShouldProcessTimeframe(utcNow, tfMinutes, pollIntervalMinutes);
        }).ToList();
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

    /// <summary>
    /// Process a single pair+TF config: detect zones, compute trend, persist, notify.
    /// </summary>
    private async Task ProcessConfigAsync(PairConfig config, CancellationToken cancellationToken)
    {
        var instrumentName = config.Instrument;
        var granularityStr = config.ZoneGranularity;
        var instrument = Enum.Parse<InstrumentName>(instrumentName, ignoreCase: true);
        var zoneGranularity = Enum.Parse<CandlestickGranularity>(granularityStr, ignoreCase: true);
        var trendGranularity = Enum.Parse<CandlestickGranularity>(config.TrendGranularity, ignoreCase: true);

        _logger.LogDebug("Processing {Instrument} {Granularity} (config v{Version})",
            instrumentName, granularityStr, config.ConfigVersion);

        // Check if config version changed — clear old zones first
        var currentStatus = await _configStore.GetStatusAsync(instrumentName, granularityStr, cancellationToken);
        if (currentStatus != null && currentStatus.ConfigVersionProcessed != config.ConfigVersion)
        {
            _logger.LogInformation("Config version changed for {Instrument} {Granularity} (v{Old}→v{New}), clearing old zones",
                instrumentName, granularityStr, currentStatus.ConfigVersionProcessed, config.ConfigVersion);
            await _zoneStore.ClearZonesAsync(instrumentName, granularityStr, cancellationToken);
        }

        // 1. Get candles from cache (incremental fetch)
        var candles = await _candleCache.GetCandlesAsync(instrument, zoneGranularity, cancellationToken);
        if (candles.Count == 0)
        {
            _logger.LogWarning("No candles for {Instrument} {Granularity}", instrumentName, zoneGranularity);
            return;
        }

        // 2. Run ZoneManager with per-config ZoneConfiguration
        var zoneConfig = config.ToZoneConfiguration();
        var zoneManager = ZoneManager.Create(candles, zoneConfig);
        var freshZones = zoneManager.GetSupplyZones().Concat(zoneManager.GetDemandZones()).ToList();

        // 3. Load previously persisted zones
        var persistedZones = await _zoneStore.GetZonesAsync(instrumentName, granularityStr, cancellationToken);

        // 4. Find new zones (not in persisted set)
        var persistedKeys = new HashSet<string>(persistedZones.Select(GetZoneKey));
        var newZones = freshZones.Where(z => !persistedKeys.Contains(GetZoneKey(z))).ToList();

        // 5. Persist all fresh zones (updates Freshness, Worked, SubZone for existing ones)
        await _zoneStore.UpsertZonesAsync(instrumentName, granularityStr, freshZones, cancellationToken);

        // 6. Compute and persist trend using per-config TrendConfiguration
        var trendConfig = config.ToTrendConfiguration();
        var trend = await GetTrendAsync(instrument, trendGranularity, trendConfig, cancellationToken);
        await _zoneStore.UpsertTrendAsync(instrumentName, granularityStr, trend, cancellationToken);

        _logger.LogInformation("{Instrument} {Granularity}: {Total} zones ({New} new, {Supply} supply, {Demand} demand), trend={Trend}",
            instrumentName, granularityStr, freshZones.Count, newZones.Count,
            freshZones.Count(z => z.Type == ZoneType.Supply),
            freshZones.Count(z => z.Type == ZoneType.Demand),
            trend);

        // 7. Send notifications if email is enabled for this config and there are new active zones
        if (newZones.Count > 0 && config.EmailEnabled)
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

        // 8. Update pair status
        await _configStore.UpsertStatusAsync(new PairStatus
        {
            Instrument = instrumentName,
            ZoneGranularity = granularityStr,
            LastProcessedUtc = DateTime.UtcNow,
            ConfigVersionProcessed = config.ConfigVersion,
            ZoneCount = freshZones.Count,
            Trend = trend
        }, cancellationToken);
    }

    private async Task<string> GetTrendAsync(
        InstrumentName instrument,
        CandlestickGranularity trendGranularity,
        TrendConfiguration trendConfig,
        CancellationToken cancellationToken)
    {
        try
        {
            var trendCandles = await _candleCache.GetCandlesAsync(instrument, trendGranularity, cancellationToken);
            var trendManager = TrendManager.Create(trendCandles, trendConfig);
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
