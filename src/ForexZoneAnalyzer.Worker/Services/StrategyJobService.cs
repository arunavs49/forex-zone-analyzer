using System.Globalization;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Data.Tables;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;
using GeriRemenyi.Oanda.V20.Client.Model;
using ZoneAnalyzer.PatternAnalysis;
using ZoneAnalyzer.PatternAnalysis.Backtesting;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// Background service that polls the strategy-jobs queue and executes optimization runs.
/// Stores results in the strategyruns Table Storage table.
/// </summary>
public class StrategyJobService : BackgroundService
{
    private readonly QueueClient _queueClient;
    private readonly TableClient _runsTable;
    private readonly ICandleStorageCache _candleCache;
    private readonly IConfigStore _configStore;
    private readonly ILogger<StrategyJobService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int MaxDequeueCount = 3; // Poison after 3 failures

    public StrategyJobService(
        IConfiguration config,
        ICandleStorageCache candleCache,
        IConfigStore configStore,
        ILogger<StrategyJobService> logger)
    {
        _candleCache = candleCache;
        _configStore = configStore;
        _logger = logger;

        var connectionString = config["Storage:ConnectionString"];
        var clientOptions = new TableClientOptions();
        clientOptions.Retry.MaxRetries = 5;
        clientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;

        if (!string.IsNullOrEmpty(connectionString))
        {
            _queueClient = new QueueClient(connectionString, "strategy-jobs");
            _runsTable = new TableClient(connectionString, "strategyruns", clientOptions);
        }
        else
        {
            var accountName = config["Storage:AccountName"];
            var credential = new DefaultAzureCredential();
            _queueClient = new QueueClient(
                new Uri($"https://{accountName}.queue.core.windows.net/strategy-jobs"),
                credential);
            var endpoint = new Uri($"https://{accountName}.table.core.windows.net");
            _runsTable = new TableClient(endpoint, "strategyruns", credential, clientOptions);
        }

        _queueClient.CreateIfNotExists();
        _runsTable.CreateIfNotExists();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StrategyJobService started — polling strategy-jobs queue");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _queueClient.ReceiveMessageAsync(
                    visibilityTimeout: TimeSpan.FromMinutes(30),
                    cancellationToken: stoppingToken);

                if (response?.Value != null)
                {
                    var message = response.Value;

                    if (message.DequeueCount > MaxDequeueCount)
                    {
                        _logger.LogWarning("Poisoning message after {Count} attempts: {Id}",
                            message.DequeueCount, message.MessageId);
                        await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                        continue;
                    }

                    await ProcessJobAsync(message.Body.ToString(), stoppingToken);
                    await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                }
                else
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing strategy job");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(string messageBody, CancellationToken cancellationToken)
    {
        var job = JsonSerializer.Deserialize<StrategyJobMessage>(messageBody);
        if (job == null)
        {
            _logger.LogWarning("Invalid job message: {Body}", messageBody);
            return;
        }

        var partitionKey = $"{job.Instrument}_{job.Granularity}";
        _logger.LogInformation("Processing strategy run {RunId} for {Partition}", job.RunId, partitionKey);

        // Update status to Running
        await UpdateRunStatus(partitionKey, job.RunId, "Running", cancellationToken);

        try
        {
            // Parse enums
            if (!Enum.TryParse<InstrumentName>(job.Instrument, out var instrument))
                throw new InvalidOperationException($"Invalid instrument: {job.Instrument}");
            if (!Enum.TryParse<CandlestickGranularity>(job.Granularity, out var zoneGranularity))
                throw new InvalidOperationException($"Invalid granularity: {job.Granularity}");

            // Determine trend granularity from config or use default mapping
            var trendGranularity = GetTrendGranularity(job.Granularity);

            // Calculate date range
            var to = DateTime.UtcNow;
            var from = to.AddMonths(-job.LookbackMonths);

            // Fetch candle data (using persistent cache)
            _logger.LogInformation("Fetching zone candles for {Partition}: {From} to {To}",
                partitionKey, from, to);
            var zoneCandles = await _candleCache.GetCandlesAsync(
                instrument, zoneGranularity, from, to, cancellationToken);

            _logger.LogInformation("Fetching trend candles for {Instrument} {TrendGranularity}",
                instrument, trendGranularity);
            var trendCandles = await _candleCache.GetCandlesAsync(
                instrument, trendGranularity, from, to, cancellationToken);

            _logger.LogInformation("Running optimizer: {ZoneCandles} zone candles, {TrendCandles} trend candles",
                zoneCandles.Count, trendCandles.Count);

            // Run optimization
            var backtestConfig = new BacktestConfig
            {
                SpreadAssumption = GetSpreadForInstrument(job.Instrument),
                FilterByTrend = true
            };

            var optimizer = new StrategyOptimizer(backtestConfig);
            var result = optimizer.Optimize(
                zoneCandles, trendCandles, topN: 10,
                progress: (done, total) => _logger.LogDebug("Optimizer progress: {Done}/{Total}", done, total),
                cancellationToken: cancellationToken);

            // Store results
            await StoreResults(partitionKey, job.RunId, result, job.LookbackMonths, cancellationToken);

            _logger.LogInformation("Strategy run {RunId} completed: {Scored}/{Total} scored, best={BestScore:F4}",
                job.RunId, result.ScoredCombinations, result.TotalCombinations,
                result.BestResult?.Score ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Strategy run {RunId} failed", job.RunId);
            await UpdateRunStatus(partitionKey, job.RunId, "Failed", cancellationToken, ex.Message);
        }
    }

    private async Task UpdateRunStatus(
        string partitionKey, string runId, string status,
        CancellationToken cancellationToken, string? error = null)
    {
        var entity = new TableEntity(partitionKey, runId)
        {
            { "Status", status },
            { "UpdatedAtUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) }
        };

        if (error != null)
            entity["Error"] = error;

        await _runsTable.UpsertEntityAsync(entity, TableUpdateMode.Merge, cancellationToken);
    }

    private async Task StoreResults(
        string partitionKey, string runId, OptimizationResult result,
        int lookbackMonths, CancellationToken cancellationToken)
    {
        var entity = new TableEntity(partitionKey, runId)
        {
            { "Status", "Completed" },
            { "CompletedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
            { "LookbackMonths", lookbackMonths },
            { "TotalCombos", result.TotalCombinations },
            { "EvaluatedCombos", result.EvaluatedCombinations },
            { "ScoredCombos", result.ScoredCombinations }
        };

        if (result.BestResult != null)
        {
            entity["BestScore"] = result.BestResult.Score;
            entity["BestWinRate"] = result.BestResult.WinRate;
            entity["BestTradedZones"] = result.BestResult.TradedZones;
            entity["BestAvgRR"] = result.BestResult.AverageRR;
            entity["BestZoneConfig"] = JsonSerializer.Serialize(new
            {
                result.BestResult.ZoneConfig.MinBaseLength,
                result.BestResult.ZoneConfig.MaxBaseLength,
                result.BestResult.ZoneConfig.MinLegInToBaseRangeRatio,
                result.BestResult.ZoneConfig.MinLegOutToBaseRangeRatio
            });
            entity["BestTrendConfig"] = JsonSerializer.Serialize(new
            {
                result.BestResult.TrendConfig.SwingLookback,
                result.BestResult.TrendConfig.TrendCandleCount,
                result.BestResult.TrendConfig.MinSwingPoints
            });
        }

        // Store top results as JSON
        var topSummary = result.TopResults.Select(r => new
        {
            r.Score, r.WinRate, r.TradedZones, r.AverageRR,
            r.Wins, r.Losses, r.Timeouts,
            Zone = new { r.ZoneConfig.MinBaseLength, r.ZoneConfig.MaxBaseLength,
                r.ZoneConfig.MinLegInToBaseRangeRatio, r.ZoneConfig.MinLegOutToBaseRangeRatio },
            Trend = new { r.TrendConfig.SwingLookback, r.TrendConfig.TrendCandleCount,
                r.TrendConfig.MinSwingPoints }
        });
        entity["TopResults"] = JsonSerializer.Serialize(topSummary);

        await _runsTable.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    /// <summary>Enqueue a new strategy job. Called by MCP server tools via queue message.</summary>
    public static async Task EnqueueJobAsync(
        QueueClient queueClient, TableClient runsTable,
        string instrument, string granularity, int lookbackMonths,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var partitionKey = $"{instrument}_{granularity}";

        // Check for existing active run
        var filter = $"PartitionKey eq '{partitionKey}' and (Status eq 'Queued' or Status eq 'Running')";
        await foreach (var _ in runsTable.QueryAsync<TableEntity>(filter, maxPerPage: 1, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException($"An active strategy run already exists for {instrument} {granularity}");
        }

        // Create run record
        var entity = new TableEntity(partitionKey, runId)
        {
            { "Status", "Queued" },
            { "RequestedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
            { "LookbackMonths", lookbackMonths }
        };
        await runsTable.UpsertEntityAsync(entity, cancellationToken: cancellationToken);

        // Queue the job
        var message = JsonSerializer.Serialize(new StrategyJobMessage
        {
            RunId = runId,
            Instrument = instrument,
            Granularity = granularity,
            LookbackMonths = lookbackMonths
        });
        await queueClient.SendMessageAsync(message, cancellationToken);
    }

    private static CandlestickGranularity GetTrendGranularity(string zoneGranularity) => zoneGranularity switch
    {
        "M5" => CandlestickGranularity.M30,
        "M15" => CandlestickGranularity.H1,
        "M30" => CandlestickGranularity.H4,
        "H1" => CandlestickGranularity.H8,
        "H4" => CandlestickGranularity.D,
        "D" => CandlestickGranularity.W,
        _ => CandlestickGranularity.H8
    };

    private static double GetSpreadForInstrument(string instrument) => instrument switch
    {
        "EUR_USD" => 0.00010,
        "GBP_USD" => 0.00015,
        "USD_JPY" => 0.010,
        "GBP_JPY" => 0.020,
        "EUR_JPY" => 0.015,
        _ when instrument.Contains("JPY") => 0.015,
        _ => 0.00015
    };
}

public class StrategyJobMessage
{
    public string RunId { get; set; } = "";
    public string Instrument { get; set; } = "";
    public string Granularity { get; set; } = "";
    public int LookbackMonths { get; set; } = 6;
}
