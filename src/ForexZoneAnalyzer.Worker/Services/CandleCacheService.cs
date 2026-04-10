using System.Collections.Concurrent;
using System.Globalization;
using ForexZoneAnalyzer.Worker.Configuration;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;
using Microsoft.Extensions.Options;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// Maintains a sliding window of candles per instrument+granularity.
/// First poll loads CandleCacheSize candles; subsequent polls fetch only new candles incrementally.
/// </summary>
public class CandleCacheService
{
    private readonly OandaConnectionService _connectionService;
    private readonly ILogger<CandleCacheService> _logger;
    private readonly MonitorSettings _settings;
    private readonly ConcurrentDictionary<string, CandleWindow> _windows = new();

    public CandleCacheService(
        OandaConnectionService connectionService,
        ILogger<CandleCacheService> logger,
        IOptions<MonitorSettings> settings)
    {
        _connectionService = connectionService;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<List<Candlestick>> GetCandlesAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        CancellationToken cancellationToken)
    {
        var key = $"{instrument}:{granularity}";
        var window = _windows.GetOrAdd(key, _ => new CandleWindow());

        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        var inst = connection.GetInstrument(instrument);
        var pricingComponents = new[] { PricingComponent.Mid };

        List<Candlestick> fetched;

        if (window.Candles.Count == 0)
        {
            _logger.LogInformation("Initial candle load for {Key}: fetching {Count} candles", key, _settings.CandleCacheSize);
            fetched = (await inst.GetLastNCandlesAsync(granularity, _settings.CandleCacheSize, pricingComponents)).ToList();
        }
        else
        {
            var lastTime = window.Candles[^1].ParsedTime();
            // Go back by overlap count worth of candle durations
            var overlapDuration = GranularityToTimeSpan(granularity) * _settings.CandleOverlapCount;
            var fetchFrom = lastTime - overlapDuration;
            var fetchTo = DateTime.UtcNow;

            _logger.LogDebug("Incremental fetch for {Key}: from {From} to {To}", key, fetchFrom, fetchTo);
            fetched = (await inst.GetCandlesByTimeAsync(granularity, fetchFrom, fetchTo, pricingComponents)).ToList();
        }

        // Merge: deduplicate by time, keep latest version of each candle
        var merged = new Dictionary<string, Candlestick>();
        foreach (var c in window.Candles)
            merged[c.Time] = c;
        foreach (var c in fetched)
            merged[c.Time] = c;

        // Sort and trim to cache size
        window.Candles = merged.Values
            .OrderBy(c => c.ParsedTime())
            .TakeLast(_settings.CandleCacheSize)
            .ToList();

        _logger.LogDebug("Cache {Key}: {Count} candles (fetched {Fetched} new)", key, window.Candles.Count, fetched.Count);
        return window.Candles;
    }

    private static TimeSpan GranularityToTimeSpan(CandlestickGranularity g) => g switch
    {
        CandlestickGranularity.S5 => TimeSpan.FromSeconds(5),
        CandlestickGranularity.S10 => TimeSpan.FromSeconds(10),
        CandlestickGranularity.S15 => TimeSpan.FromSeconds(15),
        CandlestickGranularity.S30 => TimeSpan.FromSeconds(30),
        CandlestickGranularity.M1 => TimeSpan.FromMinutes(1),
        CandlestickGranularity.M2 => TimeSpan.FromMinutes(2),
        CandlestickGranularity.M4 => TimeSpan.FromMinutes(4),
        CandlestickGranularity.M5 => TimeSpan.FromMinutes(5),
        CandlestickGranularity.M10 => TimeSpan.FromMinutes(10),
        CandlestickGranularity.M15 => TimeSpan.FromMinutes(15),
        CandlestickGranularity.M30 => TimeSpan.FromMinutes(30),
        CandlestickGranularity.H1 => TimeSpan.FromHours(1),
        CandlestickGranularity.H2 => TimeSpan.FromHours(2),
        CandlestickGranularity.H3 => TimeSpan.FromHours(3),
        CandlestickGranularity.H4 => TimeSpan.FromHours(4),
        CandlestickGranularity.H6 => TimeSpan.FromHours(6),
        CandlestickGranularity.H8 => TimeSpan.FromHours(8),
        CandlestickGranularity.H12 => TimeSpan.FromHours(12),
        CandlestickGranularity.D => TimeSpan.FromDays(1),
        CandlestickGranularity.W => TimeSpan.FromDays(7),
        CandlestickGranularity.M => TimeSpan.FromDays(30),
        _ => TimeSpan.FromMinutes(15)
    };

    private class CandleWindow
    {
        public List<Candlestick> Candles { get; set; } = new();
    }
}

internal static class CandlestickExtensions
{
    public static DateTime ParsedTime(this Candlestick c) =>
        DateTime.Parse(c.Time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
}
