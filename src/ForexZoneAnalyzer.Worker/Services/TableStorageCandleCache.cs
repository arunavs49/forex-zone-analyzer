using System.Globalization;
using Azure.Data.Tables;
using Azure.Identity;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// Table Storage-backed candle cache for historical backtesting data.
/// Stores OHLCV candles with coverage metadata for efficient gap-filling.
/// </summary>
public class TableStorageCandleCache : ICandleStorageCache
{
    private readonly TableClient _candleTable;
    private readonly TableClient _metaTable;
    private readonly OandaConnectionService _connectionService;
    private readonly ILogger<TableStorageCandleCache> _logger;

    // OANDA limits: max 5000 candles per request
    private const int OandaBatchSize = 5000;

    public TableStorageCandleCache(
        IConfiguration config,
        OandaConnectionService connectionService,
        ILogger<TableStorageCandleCache> logger)
    {
        _connectionService = connectionService;
        _logger = logger;

        var connectionString = config["Storage:ConnectionString"];
        var clientOptions = new TableClientOptions();
        clientOptions.Retry.MaxRetries = 5;
        clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;

        if (!string.IsNullOrEmpty(connectionString))
        {
            _candleTable = new TableClient(connectionString, "candlecache", clientOptions);
            _metaTable = new TableClient(connectionString, "candlecachemeta", clientOptions);
        }
        else
        {
            var accountName = config["Storage:AccountName"];
            var endpoint = new Uri($"https://{accountName}.table.core.windows.net");
            var credential = new DefaultAzureCredential();
            _candleTable = new TableClient(endpoint, "candlecache", credential, clientOptions);
            _metaTable = new TableClient(endpoint, "candlecachemeta", credential, clientOptions);
        }

        _candleTable.CreateIfNotExists();
        _metaTable.CreateIfNotExists();
    }

    public async Task<List<Candlestick>> GetCandlesAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var meta = await GetCoverageAsync(instrument, granularity, cancellationToken);

        // Determine gaps that need backfilling
        var gaps = new List<(DateTime from, DateTime to)>();
        if (meta == null)
        {
            gaps.Add((from, to));
        }
        else
        {
            if (from < meta.CachedFromUtc)
                gaps.Add((from, meta.CachedFromUtc));
            if (to > meta.CachedToUtc)
                gaps.Add((meta.CachedToUtc, to));
        }

        // Backfill each gap from OANDA
        foreach (var gap in gaps)
        {
            await BackfillRangeAsync(instrument, granularity, gap.from, gap.to, cancellationToken);
        }

        // Read from Table Storage
        var fromKey = from.ToString("o", CultureInfo.InvariantCulture);
        var toKey = to.ToString("o", CultureInfo.InvariantCulture);

        var candles = new List<Candlestick>();
        var filter = $"PartitionKey eq '{partitionKey}' and RowKey ge '{fromKey}' and RowKey le '{toKey}'";

        await foreach (var entity in _candleTable.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            candles.Add(EntityToCandle(entity));
        }

        return candles.OrderBy(c => c.ParsedTime()).ToList();
    }

    public async Task<CandleCacheMeta?> GetCoverageAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        CancellationToken cancellationToken)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var response = await _metaTable.GetEntityIfExistsAsync<TableEntity>(
            partitionKey, "_meta_", cancellationToken: cancellationToken);

        if (!response.HasValue || response.Value == null)
            return null;

        var entity = response.Value;
        return new CandleCacheMeta(
            entity.GetDateTime("CachedFromUtc") ?? DateTime.MinValue,
            entity.GetDateTime("CachedToUtc") ?? DateTime.MinValue,
            entity.GetInt32("CandleCount") ?? 0,
            entity.GetDateTime("LastBackfillUtc") ?? DateTime.MinValue
        );
    }

    private async Task BackfillRangeAsync(
        InstrumentName instrument,
        CandlestickGranularity granularity,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var partitionKey = $"{instrument}_{granularity}";
        _logger.LogInformation("Backfilling candles for {Partition}: {From} to {To}", partitionKey, from, to);

        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        var inst = connection.GetInstrument(instrument);
        var pricingComponents = new[] { PricingComponent.Mid };

        // Fetch in batches (OANDA 5000 candle limit)
        var currentFrom = from;
        var totalStored = 0;

        while (currentFrom < to)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchTo = to;
            var fetched = (await inst.GetCandlesByTimeAsync(granularity, currentFrom, batchTo, pricingComponents)).ToList();

            if (fetched.Count == 0)
                break;

            // Store only complete candles
            var complete = fetched.Where(c => c.Complete).ToList();

            // Batch upsert to Table Storage (100 per batch — Table Storage limit)
            foreach (var batch in complete.Chunk(100))
            {
                var actions = batch.Select(c =>
                {
                    var entity = CandleToEntity(partitionKey, c);
                    return new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity);
                }).ToList();

                await _candleTable.SubmitTransactionAsync(actions, cancellationToken);
                totalStored += actions.Count;
            }

            // Move past the last fetched candle
            var lastTime = fetched.Max(c => c.ParsedTime());
            if (lastTime <= currentFrom)
                break; // No progress — avoid infinite loop
            currentFrom = lastTime.AddSeconds(1);
        }

        // Update metadata
        await UpdateMetaAsync(partitionKey, cancellationToken);
        _logger.LogInformation("Backfilled {Count} candles for {Partition}", totalStored, partitionKey);
    }

    private async Task UpdateMetaAsync(string partitionKey, CancellationToken cancellationToken)
    {
        // Query min/max RowKey and count
        DateTime? minTime = null, maxTime = null;
        int count = 0;

        await foreach (var entity in _candleTable.QueryAsync<TableEntity>(
            $"PartitionKey eq '{partitionKey}'",
            select: new[] { "RowKey" },
            cancellationToken: cancellationToken))
        {
            var time = DateTime.Parse(entity.RowKey!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            if (minTime == null || time < minTime) minTime = time;
            if (maxTime == null || time > maxTime) maxTime = time;
            count++;
        }

        if (minTime == null || maxTime == null) return;

        var metaEntity = new TableEntity(partitionKey, "_meta_")
        {
            { "CachedFromUtc", DateTime.SpecifyKind(minTime.Value, DateTimeKind.Utc) },
            { "CachedToUtc", DateTime.SpecifyKind(maxTime.Value, DateTimeKind.Utc) },
            { "CandleCount", count },
            { "LastBackfillUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) }
        };

        await _metaTable.UpsertEntityAsync(metaEntity, TableUpdateMode.Replace, cancellationToken);
    }

    private static TableEntity CandleToEntity(string partitionKey, Candlestick c)
    {
        var time = c.ParsedTime();
        var rowKey = time.ToString("o", CultureInfo.InvariantCulture);

        return new TableEntity(partitionKey, rowKey)
        {
            { "Open", c.Mid?.O ?? 0 },
            { "High", c.Mid?.H ?? 0 },
            { "Low", c.Mid?.L ?? 0 },
            { "Close", c.Mid?.C ?? 0 },
            { "Volume", c.Volume },
            { "Complete", c.Complete }
        };
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
