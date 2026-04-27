using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Identity;
using ForexZoneAnalyzer.Worker.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// Periodically purges stale data older than the configured retention period.
/// Cleans: zones (disabled pair+TFs), strategyruns (completed/failed), candlecache, orphaned pairstatus.
/// </summary>
public class DataCleanupService : BackgroundService
{
    private readonly IConfigStore _configStore;
    private readonly TableClient _zonesTable;
    private readonly TableClient _strategyRunsTable;
    private readonly TableClient _candleCacheTable;
    private readonly TableClient _candleCacheMetaTable;
    private readonly TableClient _pairStatusTable;
    private readonly CleanupSettings _settings;
    private readonly ILogger<DataCleanupService> _logger;

    public DataCleanupService(
        IConfigStore configStore,
        IConfiguration configuration,
        IOptions<CleanupSettings> settings,
        ILogger<DataCleanupService> logger)
    {
        _configStore = configStore;
        _settings = settings.Value;
        _logger = logger;

        var connectionString = configuration["Storage:ConnectionString"];
        var clientOptions = new TableClientOptions();
        clientOptions.Retry.MaxRetries = 5;
        clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;

        TableClient CreateClient(string tableName)
        {
            if (!string.IsNullOrEmpty(connectionString))
                return new TableClient(connectionString, tableName, clientOptions);

            var accountName = configuration["Storage:AccountName"];
            var endpoint = new Uri($"https://{accountName}.table.core.windows.net");
            return new TableClient(endpoint, tableName, new DefaultAzureCredential(), clientOptions);
        }

        _zonesTable = CreateClient(configuration["Storage:TableName"] ?? "zones");
        _strategyRunsTable = CreateClient("strategyruns");
        _candleCacheTable = CreateClient("candlecache");
        _candleCacheMetaTable = CreateClient("candlecachemeta");
        _pairStatusTable = CreateClient("pairstatus");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait before first cleanup run to let other services initialize
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting data cleanup cycle");
                var cutoff = DateTimeOffset.UtcNow.AddDays(-_settings.RetentionDays);

                var enabledConfigs = await _configStore.GetEnabledConfigsAsync();
                var enabledKeys = new HashSet<string>(
                    enabledConfigs.Select(c => $"{c.Instrument}_{c.ZoneGranularity}"));

                var deleted = 0;
                deleted += await CleanupStrategyRuns(cutoff, stoppingToken);
                deleted += await CleanupZonesForDisabledPairs(cutoff, enabledKeys, stoppingToken);
                deleted += await CleanupCandleCache(cutoff, stoppingToken);
                deleted += await CleanupOrphanedStatus(enabledKeys, cutoff, stoppingToken);

                _logger.LogInformation("Cleanup complete: {Deleted} entities removed", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup cycle failed — will retry next interval");
            }

            await Task.Delay(TimeSpan.FromHours(_settings.IntervalHours), stoppingToken);
        }
    }

    private async Task<int> CleanupStrategyRuns(DateTimeOffset cutoff, CancellationToken ct)
    {
        int count = 0;
        var filter = $"Timestamp lt datetime'{cutoff:O}'";

        await foreach (var entity in _strategyRunsTable.QueryAsync<TableEntity>(
            filter: filter, select: new[] { "PartitionKey", "RowKey", "Status" }, cancellationToken: ct))
        {
            var status = entity.GetString("Status");
            if (status == "Completed" || status == "Failed")
            {
                await _strategyRunsTable.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken: ct);
                count++;
            }
        }

        if (count > 0) _logger.LogInformation("Cleaned {Count} strategy runs", count);
        return count;
    }

    private async Task<int> CleanupZonesForDisabledPairs(
        DateTimeOffset cutoff, HashSet<string> enabledKeys, CancellationToken ct)
    {
        int count = 0;
        var filter = $"Timestamp lt datetime'{cutoff:O}'";

        await foreach (var entity in _zonesTable.QueryAsync<TableEntity>(
            filter: filter, select: new[] { "PartitionKey", "RowKey" }, cancellationToken: ct))
        {
            // Partition key format: EUR_USD_H1
            var pk = entity.PartitionKey;
            if (!enabledKeys.Contains(pk))
            {
                await _zonesTable.DeleteEntityAsync(pk, entity.RowKey, cancellationToken: ct);
                count++;
            }
        }

        if (count > 0) _logger.LogInformation("Cleaned {Count} zone rows for disabled pairs", count);
        return count;
    }

    private async Task<int> CleanupCandleCache(DateTimeOffset cutoff, CancellationToken ct)
    {
        int count = 0;
        var filter = $"Timestamp lt datetime'{cutoff:O}'";

        await foreach (var entity in _candleCacheTable.QueryAsync<TableEntity>(
            filter: filter, select: new[] { "PartitionKey", "RowKey" }, cancellationToken: ct))
        {
            await _candleCacheTable.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken: ct);
            count++;
        }

        // Also clean stale meta entries
        await foreach (var entity in _candleCacheMetaTable.QueryAsync<TableEntity>(
            filter: filter, select: new[] { "PartitionKey", "RowKey" }, cancellationToken: ct))
        {
            await _candleCacheMetaTable.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken: ct);
            count++;
        }

        if (count > 0) _logger.LogInformation("Cleaned {Count} candle cache entries", count);
        return count;
    }

    private async Task<int> CleanupOrphanedStatus(
        HashSet<string> enabledKeys, DateTimeOffset cutoff, CancellationToken ct)
    {
        int count = 0;
        var filter = $"Timestamp lt datetime'{cutoff:O}'";

        await foreach (var entity in _pairStatusTable.QueryAsync<TableEntity>(
            filter: filter, select: new[] { "PartitionKey", "RowKey" }, cancellationToken: ct))
        {
            var key = $"{entity.PartitionKey}_{entity.RowKey}";
            if (!enabledKeys.Contains(key))
            {
                await _pairStatusTable.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken: ct);
                count++;
            }
        }

        if (count > 0) _logger.LogInformation("Cleaned {Count} orphaned status rows", count);
        return count;
    }
}
