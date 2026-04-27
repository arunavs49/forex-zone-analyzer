using System.ComponentModel;
using Azure.Data.Tables;
using ForexZoneAnalyzer.McpServer.Services;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.McpServer.Tools;

[McpServerToolType]
public sealed class ConfigTools
{
    [McpServerTool(Name = "get_pair_config"), Description("Get zone and trend detection configuration for a specific instrument and timeframe.")]
    public static async Task<string> GetPairConfig(
        ConfigTableClient configTableClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        CancellationToken cancellationToken = default)
    {
        var response = await configTableClient.Configs.GetEntityIfExistsAsync<TableEntity>(
            instrument, granularity, cancellationToken: cancellationToken);

        if (!response.HasValue || response.Value == null)
            return JsonConvert.SerializeObject(new { Error = $"No config found for {instrument} {granularity}" });

        var entity = response.Value;
        var config = EntityToConfig(entity);

        // Also fetch status
        var statusResponse = await configTableClient.Statuses.GetEntityIfExistsAsync<TableEntity>(
            instrument, granularity, cancellationToken: cancellationToken);

        var result = new
        {
            config.Instrument,
            config.ZoneGranularity,
            config.TrendGranularity,
            config.Enabled,
            config.EmailEnabled,
            config.MinBaseLength,
            config.MaxBaseLength,
            config.MinLegInToBaseRangeRatio,
            config.MinLegOutToBaseRangeRatio,
            config.SwingLookback,
            config.TrendCandleCount,
            config.MinSwingPoints,
            config.ConfigVersion,
            config.UpdatedAtUtc,
            LastProcessedUtc = statusResponse.HasValue ? statusResponse.Value?.GetDateTime("LastProcessedUtc") : null,
            ZoneCount = statusResponse.HasValue ? statusResponse.Value?.GetInt32("ZoneCount") : null,
            Trend = statusResponse.HasValue ? statusResponse.Value?.GetString("Trend") : null
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "update_pair_config"), Description("Create or update zone and trend detection configuration for a specific instrument and timeframe. Increments the config version, which triggers zone refresh on next Worker cycle.")]
    public static async Task<string> UpdatePairConfig(
        ConfigTableClient configTableClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        [Description("Higher timeframe for trend detection (e.g. 'H8' for H1 zones)")] string trendGranularity,
        [Description("Enable processing for this pair+TF")] bool enabled,
        [Description("Enable email alerts for new zones")] bool emailEnabled,
        [Description("Minimum base candle count (default 1)")] int minBaseLength = 1,
        [Description("Maximum base candle count (default 6)")] int maxBaseLength = 6,
        [Description("Minimum leg-in to base range ratio (default 1.0)")] double minLegInToBaseRangeRatio = 1.0,
        [Description("Minimum leg-out to base range ratio (default 1.0)")] double minLegOutToBaseRangeRatio = 1.0,
        [Description("Swing lookback candles for trend detection (default 3)")] int swingLookback = 3,
        [Description("Number of candles for trend analysis (default 60)")] int trendCandleCount = 60,
        [Description("Minimum swing points for trend determination (default 2)")] int minSwingPoints = 2,
        CancellationToken cancellationToken = default)
    {
        // Read existing to get current version
        var existing = await configTableClient.Configs.GetEntityIfExistsAsync<TableEntity>(
            instrument, granularity, cancellationToken: cancellationToken);

        var version = 1;
        if (existing.HasValue && existing.Value != null)
            version = (existing.Value.GetInt32("ConfigVersion") ?? 0) + 1;

        var entity = new TableEntity(instrument, granularity)
        {
            { "Enabled", enabled },
            { "EmailEnabled", emailEnabled },
            { "TrendGranularity", trendGranularity },
            { "MinBaseLength", minBaseLength },
            { "MaxBaseLength", maxBaseLength },
            { "MinLegInToBaseRangeRatio", minLegInToBaseRangeRatio },
            { "MinLegOutToBaseRangeRatio", minLegOutToBaseRangeRatio },
            { "SwingLookback", swingLookback },
            { "TrendCandleCount", trendCandleCount },
            { "MinSwingPoints", minSwingPoints },
            { "ConfigVersion", version },
            { "UpdatedAtUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) }
        };

        await configTableClient.Configs.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        return JsonConvert.SerializeObject(new
        {
            Status = "Updated",
            Instrument = instrument,
            Granularity = granularity,
            ConfigVersion = version
        }, Formatting.Indented);
    }

    [McpServerTool(Name = "set_pair_enabled"), Description("Enable or disable zone processing for a specific instrument and timeframe.")]
    public static async Task<string> SetPairEnabled(
        ConfigTableClient configTableClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        [Description("Enable (true) or disable (false) processing")] bool enabled,
        CancellationToken cancellationToken = default)
    {
        var response = await configTableClient.Configs.GetEntityIfExistsAsync<TableEntity>(
            instrument, granularity, cancellationToken: cancellationToken);

        if (!response.HasValue || response.Value == null)
            return JsonConvert.SerializeObject(new { Error = $"No config found for {instrument} {granularity}. Create one first with update_pair_config." });

        var entity = response.Value;
        entity["Enabled"] = enabled;
        entity["ConfigVersion"] = (entity.GetInt32("ConfigVersion") ?? 0) + 1;
        entity["UpdatedAtUtc"] = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        await configTableClient.Configs.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        return JsonConvert.SerializeObject(new
        {
            Status = "Updated",
            Instrument = instrument,
            Granularity = granularity,
            Enabled = enabled,
            ConfigVersion = entity.GetInt32("ConfigVersion")
        }, Formatting.Indented);
    }

    [McpServerTool(Name = "set_pair_email_enabled"), Description("Enable or disable email alerts for new zones on a specific instrument and timeframe.")]
    public static async Task<string> SetPairEmailEnabled(
        ConfigTableClient configTableClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        [Description("Enable (true) or disable (false) email alerts")] bool emailEnabled,
        CancellationToken cancellationToken = default)
    {
        var response = await configTableClient.Configs.GetEntityIfExistsAsync<TableEntity>(
            instrument, granularity, cancellationToken: cancellationToken);

        if (!response.HasValue || response.Value == null)
            return JsonConvert.SerializeObject(new { Error = $"No config found for {instrument} {granularity}. Create one first with update_pair_config." });

        var entity = response.Value;
        entity["EmailEnabled"] = emailEnabled;
        entity["UpdatedAtUtc"] = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        await configTableClient.Configs.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        return JsonConvert.SerializeObject(new
        {
            Status = "Updated",
            Instrument = instrument,
            Granularity = granularity,
            EmailEnabled = emailEnabled
        }, Formatting.Indented);
    }

    [McpServerTool(Name = "list_pair_configs"), Description("List all pair+timeframe configurations with their enabled/disabled status and last processing info.")]
    public static async Task<string> ListPairConfigs(
        ConfigTableClient configTableClient,
        CancellationToken cancellationToken = default)
    {
        var configs = new List<object>();

        // Load all statuses into a lookup
        var statusLookup = new Dictionary<string, TableEntity>();
        await foreach (var status in configTableClient.Statuses.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
        {
            statusLookup[$"{status.PartitionKey}_{status.RowKey}"] = status;
        }

        await foreach (var entity in configTableClient.Configs.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
        {
            var key = $"{entity.PartitionKey}_{entity.RowKey}";
            statusLookup.TryGetValue(key, out var status);

            configs.Add(new
            {
                Instrument = entity.PartitionKey,
                ZoneGranularity = entity.RowKey,
                TrendGranularity = entity.GetString("TrendGranularity"),
                Enabled = entity.GetBoolean("Enabled") ?? false,
                EmailEnabled = entity.GetBoolean("EmailEnabled") ?? false,
                ConfigVersion = entity.GetInt32("ConfigVersion") ?? 1,
                UpdatedAtUtc = entity.GetDateTime("UpdatedAtUtc"),
                LastProcessedUtc = status?.GetDateTime("LastProcessedUtc"),
                ZoneCount = status?.GetInt32("ZoneCount"),
                Trend = status?.GetString("Trend")
            });
        }

        return JsonConvert.SerializeObject(new
        {
            TotalConfigs = configs.Count,
            Configs = configs
        }, Formatting.Indented);
    }

    [McpServerTool(Name = "get_pair_status"), Description("Get the processing status for a specific instrument and timeframe including last processed time, zone count, and trend.")]
    public static async Task<string> GetPairStatus(
        ConfigTableClient configTableClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        CancellationToken cancellationToken = default)
    {
        var response = await configTableClient.Statuses.GetEntityIfExistsAsync<TableEntity>(
            instrument, granularity, cancellationToken: cancellationToken);

        if (!response.HasValue || response.Value == null)
            return JsonConvert.SerializeObject(new { Error = $"No status found for {instrument} {granularity}. It may not have been processed yet." });

        var entity = response.Value;
        var result = new
        {
            Instrument = instrument,
            Granularity = granularity,
            LastProcessedUtc = entity.GetDateTime("LastProcessedUtc"),
            ConfigVersionProcessed = entity.GetInt32("ConfigVersionProcessed"),
            ZoneCount = entity.GetInt32("ZoneCount"),
            Trend = entity.GetString("Trend")
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    private static dynamic EntityToConfig(TableEntity entity) => new
    {
        Instrument = entity.PartitionKey,
        ZoneGranularity = entity.RowKey,
        TrendGranularity = entity.GetString("TrendGranularity") ?? "",
        Enabled = entity.GetBoolean("Enabled") ?? false,
        EmailEnabled = entity.GetBoolean("EmailEnabled") ?? false,
        MinBaseLength = entity.GetInt32("MinBaseLength") ?? 1,
        MaxBaseLength = entity.GetInt32("MaxBaseLength") ?? 6,
        MinLegInToBaseRangeRatio = entity.GetDouble("MinLegInToBaseRangeRatio") ?? 1.0,
        MinLegOutToBaseRangeRatio = entity.GetDouble("MinLegOutToBaseRangeRatio") ?? 1.0,
        SwingLookback = entity.GetInt32("SwingLookback") ?? 3,
        TrendCandleCount = entity.GetInt32("TrendCandleCount") ?? 60,
        MinSwingPoints = entity.GetInt32("MinSwingPoints") ?? 2,
        ConfigVersion = entity.GetInt32("ConfigVersion") ?? 1,
        UpdatedAtUtc = entity.GetDateTime("UpdatedAtUtc")
    };
}
