using Azure.Data.Tables;
using Azure.Identity;
using GeriRemenyi.Oanda.V20.Client.Model;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.Worker.Services;

public class TableStorageZoneStore : IZoneStore
{
    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageZoneStore> _logger;

    public TableStorageZoneStore(IConfiguration configuration, ILogger<TableStorageZoneStore> logger)
    {
        _logger = logger;
        var connectionString = configuration["Storage:ConnectionString"];
        var tableName = configuration["Storage:TableName"] ?? "zones";

        var clientOptions = new TableClientOptions();
        clientOptions.Retry.MaxRetries = 5;
        clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;
        clientOptions.Retry.Delay = TimeSpan.FromSeconds(1);
        clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(30);
        clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(60);

        if (!string.IsNullOrEmpty(connectionString))
        {
            _tableClient = new TableClient(connectionString, tableName, clientOptions);
        }
        else
        {
            var storageAccountName = configuration["Storage:AccountName"];
            var endpoint = new Uri($"https://{storageAccountName}.table.core.windows.net");
            _tableClient = new TableClient(endpoint, tableName, new DefaultAzureCredential(), clientOptions);
        }

        _tableClient.CreateIfNotExists();
    }

    public async Task<List<Zone>> GetZonesAsync(string instrument, string granularity, CancellationToken cancellationToken)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var zones = new List<Zone>();

        try
        {
            await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                filter: $"PartitionKey eq '{partitionKey}'",
                cancellationToken: cancellationToken))
            {
                var zone = DeserializeZone(entity);
                if (zone != null)
                    zones.Add(zone);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Table Storage query failed for {Partition}, treating as empty", partitionKey);
            return new List<Zone>();
        }

        _logger.LogDebug("Loaded {Count} zones from Table Storage for {Partition}", zones.Count, partitionKey);
        return zones;
    }

    public async Task UpsertZonesAsync(string instrument, string granularity, List<Zone> zones, CancellationToken cancellationToken)
    {
        var partitionKey = $"{instrument}_{granularity}";

        foreach (var zone in zones)
        {
            var entity = SerializeZone(partitionKey, zone);
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        }

        _logger.LogDebug("Upserted {Count} zones to Table Storage for {Partition}", zones.Count, partitionKey);
    }

    public async Task<string?> GetTrendAsync(string instrument, string granularity, CancellationToken cancellationToken)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var rowKey = "_trend_";

        try
        {
            var response = await _tableClient.GetEntityIfExistsAsync<TableEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            if (response.HasValue)
            {
                return response.Value?.GetString("Trend");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Table Storage trend query failed for {Partition}", partitionKey);
        }

        return null;
    }

    public async Task UpsertTrendAsync(string instrument, string granularity, string trend, CancellationToken cancellationToken)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var entity = new TableEntity(partitionKey, "_trend_")
        {
            { "Trend", trend },
            { "UpdatedAt", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) }
        };
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        _logger.LogDebug("Upserted trend '{Trend}' to Table Storage for {Partition}", trend, partitionKey);
    }

    private static string GetRowKey(Zone zone)
    {
        // Stable identity: Type + StartTime + base range
        var raw = $"{zone.Type}_{zone.StartTime:O}_{zone.BaseRangeHigh}_{zone.BaseRangeLow}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw))
            .Replace("/", "_").Replace("+", "-").TrimEnd('=');
    }

    private static TableEntity SerializeZone(string partitionKey, Zone zone)
    {
        return new TableEntity(partitionKey, GetRowKey(zone))
        {
            { "Type", zone.Type.ToString() },
            { "StartTime", DateTime.SpecifyKind(zone.StartTime, DateTimeKind.Utc) },
            { "EndTime", DateTime.SpecifyKind(zone.EndTime, DateTimeKind.Utc) },
            { "LegInStartPrice", zone.LegInStartPrice },
            { "LegInEndPrice", zone.LegInEndPrice },
            { "LegOutStartPrice", zone.LegOutStartPrice },
            { "LegOutEndPrice", zone.LegOutEndPrice },
            { "BaseRangeHigh", zone.BaseRangeHigh },
            { "BaseRangeLow", zone.BaseRangeLow },
            { "BaseCandleCount", zone.BaseCandleCount },
            { "Freshness", zone.Freshness.ToString() },
            { "Worked", zone.Worked },
            { "SubZone", zone.SubZone }
        };
    }

    private static Zone? DeserializeZone(TableEntity entity)
    {
        try
        {
            return new Zone
            {
                Type = Enum.Parse<ZoneType>(entity.GetString("Type")),
                StartTime = entity.GetDateTimeOffset("StartTime")?.LocalDateTime ?? DateTime.MinValue,
                EndTime = entity.GetDateTimeOffset("EndTime")?.LocalDateTime ?? DateTime.MinValue,
                LegInStartPrice = entity.GetDouble("LegInStartPrice") ?? 0,
                LegInEndPrice = entity.GetDouble("LegInEndPrice") ?? 0,
                LegOutStartPrice = entity.GetDouble("LegOutStartPrice") ?? 0,
                LegOutEndPrice = entity.GetDouble("LegOutEndPrice") ?? 0,
                BaseRangeHigh = entity.GetDouble("BaseRangeHigh") ?? 0,
                BaseRangeLow = entity.GetDouble("BaseRangeLow") ?? 0,
                BaseCandleCount = entity.GetInt32("BaseCandleCount") ?? 0,
                Freshness = Enum.Parse<ZoneFreshness>(entity.GetString("Freshness") ?? "Untested"),
                Worked = entity.GetBoolean("Worked"),
                SubZone = entity.GetBoolean("SubZone") ?? false
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
