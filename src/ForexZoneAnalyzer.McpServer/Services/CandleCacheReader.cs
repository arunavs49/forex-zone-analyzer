using System.Globalization;
using Azure.Data.Tables;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.McpServer.Services;

/// <summary>
/// Read-only client for the worker's candle cache in Table Storage.
/// Returns cached candles and coverage metadata so callers can fill gaps from OANDA.
/// </summary>
public class CandleCacheReader
{
    private readonly TableClient _candleTable;
    private readonly TableClient _metaTable;

    public CandleCacheReader(TableClient candleTable, TableClient metaTable)
    {
        _candleTable = candleTable;
        _metaTable = metaTable;
    }

    /// <summary>
    /// Get cached candles for a partition, ordered by time ascending.
    /// Returns empty list if no cached data.
    /// </summary>
    public virtual async Task<List<Candlestick>> GetCachedCandlesAsync(
        string instrument, string granularity,
        DateTime from, DateTime to,
        CancellationToken ct)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var fromKey = from.ToString("o", CultureInfo.InvariantCulture);
        var toKey = to.ToString("o", CultureInfo.InvariantCulture);

        var filter = $"PartitionKey eq '{partitionKey}' and RowKey ge '{fromKey}' and RowKey le '{toKey}'";
        var candles = new List<Candlestick>();

        await foreach (var entity in _candleTable.QueryAsync<TableEntity>(filter, cancellationToken: ct))
        {
            candles.Add(EntityToCandle(entity));
        }

        return candles.OrderBy(c => DateTime.Parse(c.Time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)).ToList();
    }

    /// <summary>
    /// Get coverage metadata for a partition. Returns null if nothing cached.
    /// </summary>
    public virtual async Task<(DateTime from, DateTime to, int count)?> GetCoverageAsync(
        string instrument, string granularity, CancellationToken ct)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var response = await _metaTable.GetEntityIfExistsAsync<TableEntity>(
            partitionKey, "_meta_", cancellationToken: ct);

        if (!response.HasValue || response.Value == null)
            return null;

        var entity = response.Value;
        return (
            entity.GetDateTime("CachedFromUtc") ?? DateTime.MinValue,
            entity.GetDateTime("CachedToUtc") ?? DateTime.MinValue,
            entity.GetInt32("CandleCount") ?? 0
        );
    }

    private static Candlestick EntityToCandle(TableEntity entity)
    {
        return new Candlestick(
            time: entity.RowKey,
            mid: new CandlestickData(
                o: entity.GetDouble("Open") ?? 0,
                h: entity.GetDouble("High") ?? 0,
                l: entity.GetDouble("Low") ?? 0,
                c: entity.GetDouble("Close") ?? 0
            ),
            volume: entity.GetInt32("Volume") ?? 0,
            complete: entity.GetBoolean("Complete") ?? true
        );
    }
}
