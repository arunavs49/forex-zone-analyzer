using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ForexZoneAnalyzer.McpServer.Services;
using ForexZoneAnalyzer.McpServer.Tools;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForexZoneAnalyzer.McpServer.Test;

public class StrategyToolsTests
{
    private readonly Mock<TableClient> _runsTableMock;
    private readonly Mock<QueueClient> _queueMock;
    private readonly Mock<TableClient> _configsTableMock;
    private readonly Mock<TableClient> _statusesTableMock;
    private readonly StrategyTableClient _strategyClient;
    private readonly ConfigTableClient _configTableClient;

    public StrategyToolsTests()
    {
        _runsTableMock = new Mock<TableClient>();
        _queueMock = new Mock<QueueClient>();
        _configsTableMock = new Mock<TableClient>();
        _statusesTableMock = new Mock<TableClient>();
        _strategyClient = new StrategyTableClient(_runsTableMock.Object, _queueMock.Object);
        _configTableClient = new ConfigTableClient(_configsTableMock.Object, _statusesTableMock.Object);
    }

    #region StartStrategyRun

    [Fact]
    public async Task StartStrategyRun_QueuesJob_AndReturnsRunId()
    {
        // No active runs exist
        _runsTableMock
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages(new[]
            {
                Page<TableEntity>.FromValues(new List<TableEntity>(), null, Mock.Of<Response>())
            }));

        _runsTableMock
            .Setup(x => x.UpsertEntityAsync(It.IsAny<TableEntity>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        _queueMock
            .Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        var result = await StrategyTools.StartStrategyRun(
            _strategyClient, "EUR_USD", "H1", 6);

        var json = JObject.Parse(result);
        Assert.Equal("Queued", json["Status"]!.Value<string>());
        Assert.NotNull(json["RunId"]!.Value<string>());
        Assert.Equal("EUR_USD", json["Instrument"]!.Value<string>());
        Assert.Equal(6, json["LookbackMonths"]!.Value<int>());

        _runsTableMock.Verify(x => x.UpsertEntityAsync(
            It.Is<TableEntity>(e => e.GetString("Status") == "Queued"),
            It.IsAny<TableUpdateMode>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _queueMock.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartStrategyRun_ReturnsError_WhenActiveRunExists()
    {
        var activeRun = new TableEntity("EUR_USD_H1", "abc123")
        {
            { "Status", "Running" }
        };

        _runsTableMock
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages(new[]
            {
                Page<TableEntity>.FromValues(new List<TableEntity> { activeRun }, null, Mock.Of<Response>())
            }));

        var result = await StrategyTools.StartStrategyRun(
            _strategyClient, "EUR_USD", "H1", 6);

        var json = JObject.Parse(result);
        Assert.Contains("active strategy run already exists", json["Error"]!.Value<string>());
    }

    [Fact]
    public async Task StartStrategyRun_ReturnsError_WhenLookbackOutOfRange()
    {
        var result = await StrategyTools.StartStrategyRun(
            _strategyClient, "EUR_USD", "H1", 25);

        var json = JObject.Parse(result);
        Assert.Contains("lookbackMonths must be between 1 and 24", json["Error"]!.Value<string>());
    }

    #endregion

    #region GetStrategyRun

    [Fact]
    public async Task GetStrategyRun_ReturnsRunDetails_WhenQueued()
    {
        var entity = new TableEntity("EUR_USD_H1", "run123")
        {
            { "Status", "Queued" },
            { "RequestedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
            { "LookbackMonths", 6 }
        };

        _runsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD_H1", "run123", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        var result = await StrategyTools.GetStrategyRun(
            _strategyClient, "EUR_USD", "H1", "run123");

        var json = JObject.Parse(result);
        Assert.Equal("run123", json["RunId"]!.Value<string>());
        Assert.Equal("Queued", json["Status"]!.Value<string>());
        Assert.Equal(6, json["LookbackMonths"]!.Value<int>());
    }

    [Fact]
    public async Task GetStrategyRun_ReturnsResults_WhenCompleted()
    {
        var zoneConfig = """{"MinBaseLength":2,"MaxBaseLength":5,"MinLegInToBaseRangeRatio":1.2,"MinLegOutToBaseRangeRatio":0.8}""";
        var trendConfig = """{"SwingLookback":4,"TrendCandleCount":80,"MinSwingPoints":3}""";
        var topResults = """[{"Score":85.5,"WinRate":0.72}]""";

        var entity = new TableEntity("EUR_USD_H1", "run456")
        {
            { "Status", "Completed" },
            { "RequestedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
            { "CompletedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
            { "LookbackMonths", 6 },
            { "BestScore", 85.5 },
            { "BestWinRate", 0.72 },
            { "BestTradedZones", 15 },
            { "BestAvgRR", 2.1 },
            { "TotalCombos", 1000 },
            { "ScoredCombos", 800 },
            { "BestZoneConfig", zoneConfig },
            { "BestTrendConfig", trendConfig },
            { "TopResults", topResults }
        };

        _runsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD_H1", "run456", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        var result = await StrategyTools.GetStrategyRun(
            _strategyClient, "EUR_USD", "H1", "run456");

        var json = JObject.Parse(result);
        Assert.Equal("Completed", json["Status"]!.Value<string>());
        Assert.Equal(85.5, json["BestScore"]!.Value<double>());
        Assert.Equal(0.72, json["BestWinRate"]!.Value<double>());
        Assert.Equal(15, json["BestTradedZones"]!.Value<int>());
        Assert.NotNull(json["BestZoneConfig"]);
        Assert.NotNull(json["TopResults"]);
    }

    [Fact]
    public async Task GetStrategyRun_ReturnsError_WhenNotFound()
    {
        _runsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD_H1", "norun", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        var result = await StrategyTools.GetStrategyRun(
            _strategyClient, "EUR_USD", "H1", "norun");

        var json = JObject.Parse(result);
        Assert.Contains("not found", json["Error"]!.Value<string>());
    }

    #endregion

    #region ListStrategyRuns

    [Fact]
    public async Task ListStrategyRuns_ReturnsRuns()
    {
        var runs = new List<TableEntity>
        {
            new TableEntity("EUR_USD_H1", "run1")
            {
                { "Status", "Completed" },
                { "RequestedUtc", DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-2), DateTimeKind.Utc) },
                { "LookbackMonths", 6 },
                { "BestScore", 80.0 },
                { "BestWinRate", 0.65 }
            },
            new TableEntity("EUR_USD_H1", "run2")
            {
                { "Status", "Running" },
                { "RequestedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
                { "LookbackMonths", 12 }
            }
        };

        _runsTableMock
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages(new[]
            {
                Page<TableEntity>.FromValues(runs, null, Mock.Of<Response>())
            }));

        var result = await StrategyTools.ListStrategyRuns(
            _strategyClient, "EUR_USD", "H1");

        var json = JObject.Parse(result);
        Assert.Equal(2, json["TotalRuns"]!.Value<int>());
        var runsArray = json["Runs"]!.ToObject<JArray>()!;
        // Reversed order (most recent first)
        Assert.Equal("run2", runsArray[0]["RunId"]!.Value<string>());
        Assert.Equal("run1", runsArray[1]["RunId"]!.Value<string>());
    }

    #endregion

    #region ApplyStrategyResult

    [Fact]
    public async Task ApplyStrategyResult_CopiesBestConfig_ToPairConfigs()
    {
        var zoneConfig = """{"MinBaseLength":2,"MaxBaseLength":5,"MinLegInToBaseRangeRatio":1.2,"MinLegOutToBaseRangeRatio":0.8}""";
        var trendConfig = """{"SwingLookback":4,"TrendCandleCount":80,"MinSwingPoints":3}""";

        var runEntity = new TableEntity("EUR_USD_H1", "run789")
        {
            { "Status", "Completed" },
            { "BestScore", 90.0 },
            { "BestWinRate", 0.75 },
            { "BestZoneConfig", zoneConfig },
            { "BestTrendConfig", trendConfig }
        };

        _runsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD_H1", "run789", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(runEntity, Mock.Of<Response>()));

        var existingConfig = new TableEntity("EUR_USD", "H1")
        {
            { "ConfigVersion", 5 },
            { "Enabled", true },
            { "EmailEnabled", true },
            { "TrendGranularity", "H4" }
        };

        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(existingConfig, Mock.Of<Response>()));

        _configsTableMock
            .Setup(x => x.UpsertEntityAsync(It.IsAny<TableEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var result = await StrategyTools.ApplyStrategyResult(
            _configTableClient, _strategyClient, "EUR_USD", "H1", "run789");

        var json = JObject.Parse(result);
        Assert.Equal("Applied", json["Status"]!.Value<string>());
        Assert.Equal(6, json["ConfigVersion"]!.Value<int>());
        Assert.Equal("run789", json["AppliedFromRunId"]!.Value<string>());

        // Verify the upserted entity has the strategy's best config values
        _configsTableMock.Verify(x => x.UpsertEntityAsync(
            It.Is<TableEntity>(e =>
                e.GetInt32("MinBaseLength") == 2 &&
                e.GetInt32("MaxBaseLength") == 5 &&
                e.GetDouble("MinLegInToBaseRangeRatio") == 1.2 &&
                e.GetInt32("SwingLookback") == 4 &&
                e.GetInt32("TrendCandleCount") == 80 &&
                e.GetBoolean("Enabled") == true &&
                e.GetBoolean("EmailEnabled") == true &&
                e.GetString("TrendGranularity") == "H4"),
            TableUpdateMode.Replace,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyStrategyResult_ReturnsError_WhenRunNotFound()
    {
        _runsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD_H1", "norun", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        var result = await StrategyTools.ApplyStrategyResult(
            _configTableClient, _strategyClient, "EUR_USD", "H1", "norun");

        var json = JObject.Parse(result);
        Assert.Contains("not found", json["Error"]!.Value<string>());
    }

    [Fact]
    public async Task ApplyStrategyResult_ReturnsError_WhenRunNotCompleted()
    {
        var runEntity = new TableEntity("EUR_USD_H1", "runqueue")
        {
            { "Status", "Queued" }
        };

        _runsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD_H1", "runqueue", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(runEntity, Mock.Of<Response>()));

        var result = await StrategyTools.ApplyStrategyResult(
            _configTableClient, _strategyClient, "EUR_USD", "H1", "runqueue");

        var json = JObject.Parse(result);
        Assert.Contains("not completed", json["Error"]!.Value<string>());
    }

    [Fact]
    public async Task ApplyStrategyResult_CreatesNewConfig_WhenNoneExists()
    {
        var zoneConfig = """{"MinBaseLength":1,"MaxBaseLength":6,"MinLegInToBaseRangeRatio":1.0,"MinLegOutToBaseRangeRatio":1.0}""";
        var trendConfig = """{"SwingLookback":3,"TrendCandleCount":60,"MinSwingPoints":2}""";

        var runEntity = new TableEntity("EUR_USD_H1", "runnew")
        {
            { "Status", "Completed" },
            { "BestScore", 70.0 },
            { "BestWinRate", 0.60 },
            { "BestZoneConfig", zoneConfig },
            { "BestTrendConfig", trendConfig }
        };

        _runsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD_H1", "runnew", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(runEntity, Mock.Of<Response>()));

        // No existing config
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        _configsTableMock
            .Setup(x => x.UpsertEntityAsync(It.IsAny<TableEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var result = await StrategyTools.ApplyStrategyResult(
            _configTableClient, _strategyClient, "EUR_USD", "H1", "runnew");

        var json = JObject.Parse(result);
        Assert.Equal("Applied", json["Status"]!.Value<string>());
        Assert.Equal(1, json["ConfigVersion"]!.Value<int>());
    }

    #endregion
}
