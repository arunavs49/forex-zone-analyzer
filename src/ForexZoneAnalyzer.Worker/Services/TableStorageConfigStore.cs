using Azure.Data.Tables;
using ForexZoneAnalyzer.Worker.Configuration;

namespace ForexZoneAnalyzer.Worker.Services;

public class TableStorageConfigStore : IConfigStore
{
    private readonly TableClient _configClient;
    private readonly TableClient _statusClient;
    private readonly ILogger<TableStorageConfigStore> _logger;

    private List<PairConfig>? _cachedConfigs;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public TableStorageConfigStore(IConfiguration configuration, ILogger<TableStorageConfigStore> logger)
    {
        _logger = logger;
        var connectionString = configuration["Storage:ConnectionString"];

        var clientOptions = new TableClientOptions();
        clientOptions.Retry.MaxRetries = 5;
        clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;

        if (!string.IsNullOrEmpty(connectionString))
        {
            _configClient = new TableClient(connectionString, "pairconfigs", clientOptions);
            _statusClient = new TableClient(connectionString, "pairstatus", clientOptions);
        }
        else
        {
            var storageAccountName = configuration["Storage:AccountName"]
                ?? throw new InvalidOperationException("Storage:ConnectionString or Storage:AccountName required");
            var configEndpoint = new Uri($"https://{storageAccountName}.table.core.windows.net");
            var statusEndpoint = new Uri($"https://{storageAccountName}.table.core.windows.net");
            _configClient = new TableClient(configEndpoint, "pairconfigs", new Azure.Identity.DefaultAzureCredential(), clientOptions);
            _statusClient = new TableClient(statusEndpoint, "pairstatus", new Azure.Identity.DefaultAzureCredential(), clientOptions);
        }

        _configClient.CreateIfNotExists();
        _statusClient.CreateIfNotExists();
    }

    public async Task<List<PairConfig>> GetEnabledConfigsAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetAllConfigsCachedAsync(cancellationToken);
        return all.Where(c => c.Enabled).ToList();
    }

    public async Task<List<PairConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllConfigsCachedAsync(cancellationToken);
    }

    public async Task<PairConfig?> GetConfigAsync(string instrument, string granularity, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _configClient.GetEntityIfExistsAsync<TableEntity>(
                instrument, granularity, cancellationToken: cancellationToken);

            if (response.HasValue && response.Value != null)
                return EntityToConfig(response.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to get config for {Instrument} {Granularity}", instrument, granularity);
        }

        return null;
    }

    public async Task UpsertConfigAsync(PairConfig config, CancellationToken cancellationToken = default)
    {
        // Read existing to increment version
        var existing = await GetConfigAsync(config.Instrument, config.ZoneGranularity, cancellationToken);
        if (existing != null)
            config.ConfigVersion = existing.ConfigVersion + 1;
        else
            config.ConfigVersion = 1;

        config.UpdatedAtUtc = DateTime.UtcNow;

        var entity = ConfigToEntity(config);
        await _configClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        InvalidateCache();

        _logger.LogInformation("Upserted config for {Instrument} {Granularity} (version {Version})",
            config.Instrument, config.ZoneGranularity, config.ConfigVersion);
    }

    public async Task SetEnabledAsync(string instrument, string granularity, bool enabled, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(instrument, granularity, cancellationToken)
            ?? throw new InvalidOperationException($"Config not found for {instrument} {granularity}");

        config.Enabled = enabled;
        config.ConfigVersion++; // version bump so Worker knows to re-process
        config.UpdatedAtUtc = DateTime.UtcNow;

        var entity = ConfigToEntity(config);
        await _configClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        InvalidateCache();

        _logger.LogInformation("Set {Instrument} {Granularity} enabled={Enabled} (version {Version})",
            instrument, granularity, enabled, config.ConfigVersion);
    }

    public async Task SetEmailEnabledAsync(string instrument, string granularity, bool emailEnabled, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(instrument, granularity, cancellationToken)
            ?? throw new InvalidOperationException($"Config not found for {instrument} {granularity}");

        config.EmailEnabled = emailEnabled;
        config.UpdatedAtUtc = DateTime.UtcNow;

        var entity = ConfigToEntity(config);
        await _configClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        InvalidateCache();

        _logger.LogInformation("Set {Instrument} {Granularity} emailEnabled={EmailEnabled}",
            instrument, granularity, emailEnabled);
    }

    public async Task<PairStatus?> GetStatusAsync(string instrument, string granularity, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _statusClient.GetEntityIfExistsAsync<TableEntity>(
                instrument, granularity, cancellationToken: cancellationToken);

            if (response.HasValue && response.Value != null)
                return EntityToStatus(response.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to get status for {Instrument} {Granularity}", instrument, granularity);
        }

        return null;
    }

    public async Task<List<PairStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = new List<PairStatus>();
        try
        {
            await foreach (var entity in _statusClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
            {
                statuses.Add(EntityToStatus(entity));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to list statuses");
        }
        return statuses;
    }

    public async Task UpsertStatusAsync(PairStatus status, CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(status.Instrument, status.ZoneGranularity)
        {
            { "LastProcessedUtc", status.LastProcessedUtc.HasValue
                ? DateTime.SpecifyKind(status.LastProcessedUtc.Value, DateTimeKind.Utc)
                : (object?)null },
            { "ConfigVersionProcessed", status.ConfigVersionProcessed },
            { "ZoneCount", status.ZoneCount },
            { "Trend", status.Trend }
        };

        await _statusClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
        _cachedConfigs = null;
    }

    /// <summary>
    /// Check if the config table has any rows (used for seed detection).
    /// </summary>
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var _ in _configClient.QueryAsync<TableEntity>(
            maxPerPage: 1, cancellationToken: cancellationToken))
        {
            return false;
        }
        return true;
    }

    private async Task<List<PairConfig>> GetAllConfigsCachedAsync(CancellationToken cancellationToken)
    {
        if (_cachedConfigs != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedConfigs;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedConfigs != null && DateTime.UtcNow < _cacheExpiry)
                return _cachedConfigs;

            var configs = new List<PairConfig>();
            await foreach (var entity in _configClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
            {
                configs.Add(EntityToConfig(entity));
            }

            _cachedConfigs = configs;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheTtl);
            _logger.LogDebug("Refreshed config cache: {Count} configs", configs.Count);
            return configs;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static TableEntity ConfigToEntity(PairConfig config) => new(config.Instrument, config.ZoneGranularity)
    {
        { "Enabled", config.Enabled },
        { "EmailEnabled", config.EmailEnabled },
        { "TrendGranularity", config.TrendGranularity },
        { "MinBaseLength", config.MinBaseLength },
        { "MaxBaseLength", config.MaxBaseLength },
        { "MinLegInToBaseRangeRatio", config.MinLegInToBaseRangeRatio },
        { "MinLegOutToBaseRangeRatio", config.MinLegOutToBaseRangeRatio },
        { "SwingLookback", config.SwingLookback },
        { "TrendCandleCount", config.TrendCandleCount },
        { "MinSwingPoints", config.MinSwingPoints },
        { "ConfigVersion", config.ConfigVersion },
        { "UpdatedAtUtc", DateTime.SpecifyKind(config.UpdatedAtUtc, DateTimeKind.Utc) }
    };

    private static PairConfig EntityToConfig(TableEntity entity) => new()
    {
        Instrument = entity.PartitionKey,
        ZoneGranularity = entity.RowKey,
        Enabled = entity.GetBoolean("Enabled") ?? false,
        EmailEnabled = entity.GetBoolean("EmailEnabled") ?? false,
        TrendGranularity = entity.GetString("TrendGranularity") ?? "",
        MinBaseLength = entity.GetInt32("MinBaseLength") ?? 1,
        MaxBaseLength = entity.GetInt32("MaxBaseLength") ?? 6,
        MinLegInToBaseRangeRatio = entity.GetDouble("MinLegInToBaseRangeRatio") ?? 1.0,
        MinLegOutToBaseRangeRatio = entity.GetDouble("MinLegOutToBaseRangeRatio") ?? 1.0,
        SwingLookback = entity.GetInt32("SwingLookback") ?? 3,
        TrendCandleCount = entity.GetInt32("TrendCandleCount") ?? 60,
        MinSwingPoints = entity.GetInt32("MinSwingPoints") ?? 2,
        ConfigVersion = entity.GetInt32("ConfigVersion") ?? 1,
        UpdatedAtUtc = entity.GetDateTime("UpdatedAtUtc") ?? DateTime.UtcNow
    };

    private static PairStatus EntityToStatus(TableEntity entity) => new()
    {
        Instrument = entity.PartitionKey,
        ZoneGranularity = entity.RowKey,
        LastProcessedUtc = entity.GetDateTime("LastProcessedUtc"),
        ConfigVersionProcessed = entity.GetInt32("ConfigVersionProcessed") ?? 0,
        ZoneCount = entity.GetInt32("ZoneCount") ?? 0,
        Trend = entity.GetString("Trend") ?? "Unknown"
    };
}
