using System.Globalization;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// In-memory candle storage cache for development/testing.
/// Fetches from OANDA on every request (no persistence).
/// </summary>
public class InMemoryCandleStorageCache : ICandleStorageCache
{
    private readonly OandaConnectionService _connectionService;
    private readonly ILogger<InMemoryCandleStorageCache> _logger;

    public InMemoryCandleStorageCache(
        OandaConnectionService connectionService,
        ILogger<InMemoryCandleStorageCache> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    public async Task<List<Candlestick>> GetCandlesAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("InMemory candle cache: fetching {Instrument} {Granularity} from {From} to {To}",
            instrument, granularity, from, to);

        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        var inst = connection.GetInstrument(instrument);
        var pricingComponents = new[] { PricingComponent.Mid };

        var fetched = (await inst.GetCandlesByTimeAsync(granularity, from, to, pricingComponents)).ToList();
        return fetched.Where(c => c.Complete).OrderBy(c => c.ParsedTime()).ToList();
    }

    public Task<CandleCacheMeta?> GetCoverageAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        CancellationToken cancellationToken) => Task.FromResult<CandleCacheMeta?>(null);
}
