using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// Persistent candle cache backed by Azure Table Storage.
/// Used by the strategy optimizer to fetch historical data without repeated OANDA calls.
/// </summary>
public interface ICandleStorageCache
{
    /// <summary>
    /// Get candles for a range, backfilling from OANDA as needed.
    /// Returns only complete candles sorted by time ascending.
    /// </summary>
    Task<List<Candlestick>> GetCandlesAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);

    /// <summary>
    /// Get cached coverage metadata (from/to dates) without fetching candles.
    /// Returns null if no data is cached for this instrument+granularity.
    /// </summary>
    Task<CandleCacheMeta?> GetCoverageAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        CancellationToken cancellationToken);
}

public record CandleCacheMeta(
    DateTime CachedFromUtc,
    DateTime CachedToUtc,
    int CandleCount,
    DateTime LastBackfillUtc);
