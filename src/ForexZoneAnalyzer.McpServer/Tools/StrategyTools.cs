using System.ComponentModel;
using System.Text.Json;
using Azure.Data.Tables;
using ForexZoneAnalyzer.McpServer.Services;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.McpServer.Tools;

[McpServerToolType]
public sealed class StrategyTools
{
    [McpServerTool(Name = "start_strategy_run"), Description("Start a strategy optimization run for a specific instrument and timeframe. Tests thousands of zone/trend config combinations against historical data to find optimal settings. Only one active run per pair+TF.")]
    public static async Task<string> StartStrategyRun(
        StrategyTableClient strategyClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        [Description("Months of historical data to backtest (1-24, default 6)")] int lookbackMonths = 6,
        CancellationToken cancellationToken = default)
    {
        if (lookbackMonths < 1 || lookbackMonths > 24)
            return JsonConvert.SerializeObject(new { Error = "lookbackMonths must be between 1 and 24" });

        try
        {
            var runId = Guid.NewGuid().ToString("N")[..12];
            var partitionKey = $"{instrument}_{granularity}";

            // Check for existing active run
            var filter = $"PartitionKey eq '{partitionKey}' and (Status eq 'Queued' or Status eq 'Running')";
            await foreach (var _ in strategyClient.Runs.QueryAsync<TableEntity>(filter, maxPerPage: 1, cancellationToken: cancellationToken))
            {
                return JsonConvert.SerializeObject(new { Error = $"An active strategy run already exists for {instrument} {granularity}. Wait for it to complete or check status." });
            }

            // Create run record
            var entity = new TableEntity(partitionKey, runId)
            {
                { "Status", "Queued" },
                { "RequestedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
                { "LookbackMonths", lookbackMonths }
            };
            await strategyClient.Runs.UpsertEntityAsync(entity, cancellationToken: cancellationToken);

            // Queue the job
            var message = System.Text.Json.JsonSerializer.Serialize(new
            {
                RunId = runId,
                Instrument = instrument,
                Granularity = granularity,
                LookbackMonths = lookbackMonths
            });
            await strategyClient.Queue.SendMessageAsync(message, cancellationToken);

            return JsonConvert.SerializeObject(new
            {
                Status = "Queued",
                RunId = runId,
                Instrument = instrument,
                Granularity = granularity,
                LookbackMonths = lookbackMonths,
                Message = "Strategy optimization queued. Use get_strategy_run to check progress."
            }, Formatting.Indented);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { Error = ex.Message });
        }
    }

    [McpServerTool(Name = "get_strategy_run"), Description("Get the status and results of a strategy optimization run.")]
    public static async Task<string> GetStrategyRun(
        StrategyTableClient strategyClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        [Description("Run ID from start_strategy_run")] string runId,
        CancellationToken cancellationToken = default)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var response = await strategyClient.Runs.GetEntityIfExistsAsync<TableEntity>(
            partitionKey, runId, cancellationToken: cancellationToken);

        if (!response.HasValue || response.Value == null)
            return JsonConvert.SerializeObject(new { Error = $"Strategy run {runId} not found" });

        var entity = response.Value;
        var result = new Dictionary<string, object?>
        {
            ["RunId"] = runId,
            ["Instrument"] = instrument,
            ["Granularity"] = granularity,
            ["Status"] = entity.GetString("Status"),
            ["RequestedUtc"] = entity.GetDateTime("RequestedUtc")?.ToString("o"),
            ["CompletedUtc"] = entity.GetDateTime("CompletedUtc")?.ToString("o"),
            ["LookbackMonths"] = entity.GetInt32("LookbackMonths"),
            ["Error"] = entity.GetString("Error")
        };

        // Include results if completed
        if (entity.GetString("Status") == "Completed")
        {
            result["BestScore"] = entity.GetDouble("BestScore");
            result["BestWinRate"] = entity.GetDouble("BestWinRate");
            result["BestTradedZones"] = entity.GetInt32("BestTradedZones");
            result["BestAvgRR"] = entity.GetDouble("BestAvgRR");
            result["TotalCombos"] = entity.GetInt32("TotalCombos");
            result["ScoredCombos"] = entity.GetInt32("ScoredCombos");

            var zoneConfigJson = entity.GetString("BestZoneConfig");
            if (zoneConfigJson != null)
                result["BestZoneConfig"] = System.Text.Json.JsonSerializer.Deserialize<object>(zoneConfigJson);

            var trendConfigJson = entity.GetString("BestTrendConfig");
            if (trendConfigJson != null)
                result["BestTrendConfig"] = System.Text.Json.JsonSerializer.Deserialize<object>(trendConfigJson);

            var topResultsJson = entity.GetString("TopResults");
            if (topResultsJson != null)
                result["TopResults"] = System.Text.Json.JsonSerializer.Deserialize<object>(topResultsJson);
        }

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "list_strategy_runs"), Description("List all strategy optimization runs for a specific instrument and timeframe, most recent first.")]
    public static async Task<string> ListStrategyRuns(
        StrategyTableClient strategyClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        CancellationToken cancellationToken = default)
    {
        var partitionKey = $"{instrument}_{granularity}";
        var runs = new List<object>();

        await foreach (var entity in strategyClient.Runs.QueryAsync<TableEntity>(
            $"PartitionKey eq '{partitionKey}'", cancellationToken: cancellationToken))
        {
            runs.Add(new
            {
                RunId = entity.RowKey,
                Status = entity.GetString("Status"),
                RequestedUtc = entity.GetDateTime("RequestedUtc")?.ToString("o"),
                CompletedUtc = entity.GetDateTime("CompletedUtc")?.ToString("o"),
                LookbackMonths = entity.GetInt32("LookbackMonths"),
                BestScore = entity.GetDouble("BestScore"),
                BestWinRate = entity.GetDouble("BestWinRate"),
                Error = entity.GetString("Error")
            });
        }

        // Sort by requested time descending
        runs.Reverse();

        return JsonConvert.SerializeObject(new
        {
            Instrument = instrument,
            Granularity = granularity,
            TotalRuns = runs.Count,
            Runs = runs
        }, Formatting.Indented);
    }

    [McpServerTool(Name = "apply_strategy_result"), Description("Apply the best configuration from a completed strategy run to the pair's active configuration. This updates the zone and trend detection settings.")]
    public static async Task<string> ApplyStrategyResult(
        ConfigTableClient configTableClient,
        StrategyTableClient strategyClient,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Zone granularity (e.g. 'H1', 'M15')")] string granularity,
        [Description("Run ID to apply results from")] string runId,
        CancellationToken cancellationToken = default)
    {
        var partitionKey = $"{instrument}_{granularity}";

        // Get the run
        var runResponse = await strategyClient.Runs.GetEntityIfExistsAsync<TableEntity>(
            partitionKey, runId, cancellationToken: cancellationToken);

        if (!runResponse.HasValue || runResponse.Value == null)
            return JsonConvert.SerializeObject(new { Error = $"Strategy run {runId} not found" });

        var run = runResponse.Value;
        if (run.GetString("Status") != "Completed")
            return JsonConvert.SerializeObject(new { Error = $"Strategy run {runId} is not completed (status: {run.GetString("Status")})" });

        var zoneConfigJson = run.GetString("BestZoneConfig");
        var trendConfigJson = run.GetString("BestTrendConfig");
        if (zoneConfigJson == null || trendConfigJson == null)
            return JsonConvert.SerializeObject(new { Error = "No best config found in run results" });

        // Parse configs
        using var zoneDoc = System.Text.Json.JsonDocument.Parse(zoneConfigJson);
        using var trendDoc = System.Text.Json.JsonDocument.Parse(trendConfigJson);

        // Get existing config or create new
        var existing = await configTableClient.Configs.GetEntityIfExistsAsync<TableEntity>(
            instrument, granularity, cancellationToken: cancellationToken);

        var version = 1;
        var enabled = true;
        var emailEnabled = false;
        var trendGranularity = "H8"; // default

        if (existing.HasValue && existing.Value != null)
        {
            version = (existing.Value.GetInt32("ConfigVersion") ?? 0) + 1;
            enabled = existing.Value.GetBoolean("Enabled") ?? true;
            emailEnabled = existing.Value.GetBoolean("EmailEnabled") ?? false;
            trendGranularity = existing.Value.GetString("TrendGranularity") ?? "H8";
        }

        var entity = new TableEntity(instrument, granularity)
        {
            { "Enabled", enabled },
            { "EmailEnabled", emailEnabled },
            { "TrendGranularity", trendGranularity },
            { "MinBaseLength", zoneDoc.RootElement.GetProperty("MinBaseLength").GetInt32() },
            { "MaxBaseLength", zoneDoc.RootElement.GetProperty("MaxBaseLength").GetInt32() },
            { "MinLegInToBaseRangeRatio", zoneDoc.RootElement.GetProperty("MinLegInToBaseRangeRatio").GetDouble() },
            { "MinLegOutToBaseRangeRatio", zoneDoc.RootElement.GetProperty("MinLegOutToBaseRangeRatio").GetDouble() },
            { "SwingLookback", trendDoc.RootElement.GetProperty("SwingLookback").GetInt32() },
            { "TrendCandleCount", trendDoc.RootElement.GetProperty("TrendCandleCount").GetInt32() },
            { "MinSwingPoints", trendDoc.RootElement.GetProperty("MinSwingPoints").GetInt32() },
            { "ConfigVersion", version },
            { "UpdatedAtUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) }
        };

        await configTableClient.Configs.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        return JsonConvert.SerializeObject(new
        {
            Status = "Applied",
            Instrument = instrument,
            Granularity = granularity,
            ConfigVersion = version,
            AppliedFromRunId = runId,
            BestScore = run.GetDouble("BestScore"),
            BestWinRate = run.GetDouble("BestWinRate")
        }, Formatting.Indented);
    }
}
