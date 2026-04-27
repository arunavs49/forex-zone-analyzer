using Azure;
using Azure.Data.Tables;
using ForexZoneAnalyzer.McpServer.Services;
using ForexZoneAnalyzer.McpServer.Tools;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForexZoneAnalyzer.McpServer.Test;

public class ConfigToolsTests
{
    private readonly Mock<TableClient> _configsTableMock;
    private readonly Mock<TableClient> _statusesTableMock;
    private readonly ConfigTableClient _configTableClient;

    public ConfigToolsTests()
    {
        _configsTableMock = new Mock<TableClient>();
        _statusesTableMock = new Mock<TableClient>();
        _configTableClient = new ConfigTableClient(_configsTableMock.Object, _statusesTableMock.Object);
    }

    private static TableEntity CreateConfigEntity(
        string instrument = "EUR_USD",
        string granularity = "H1",
        bool enabled = true,
        bool emailEnabled = false,
        int configVersion = 1)
    {
        return new TableEntity(instrument, granularity)
        {
            { "TrendGranularity", "H8" },
            { "Enabled", enabled },
            { "EmailEnabled", emailEnabled },
            { "MinBaseLength", 1 },
            { "MaxBaseLength", 6 },
            { "MinLegInToBaseRangeRatio", 1.0 },
            { "MinLegOutToBaseRangeRatio", 1.0 },
            { "SwingLookback", 3 },
            { "TrendCandleCount", 60 },
            { "MinSwingPoints", 2 },
            { "ConfigVersion", configVersion },
            { "UpdatedAtUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) }
        };
    }

    private static TableEntity CreateStatusEntity(string instrument = "EUR_USD", string granularity = "H1")
    {
        return new TableEntity(instrument, granularity)
        {
            { "LastProcessedUtc", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) },
            { "ConfigVersionProcessed", 1 },
            { "ZoneCount", 5 },
            { "Trend", "Bullish" }
        };
    }

    #region GetPairConfig

    [Fact]
    public async Task GetPairConfig_ReturnsConfig_WhenExists()
    {
        var configEntity = CreateConfigEntity();
        var statusEntity = CreateStatusEntity();

        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(configEntity, Mock.Of<Response>()));

        _statusesTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(statusEntity, Mock.Of<Response>()));

        var result = await ConfigTools.GetPairConfig(_configTableClient, "EUR_USD", "H1");

        var json = JObject.Parse(result);
        Assert.Equal("EUR_USD", json["Instrument"]!.Value<string>());
        Assert.Equal("H1", json["ZoneGranularity"]!.Value<string>());
        Assert.Equal("H8", json["TrendGranularity"]!.Value<string>());
        Assert.True(json["Enabled"]!.Value<bool>());
        Assert.Equal(1, json["ConfigVersion"]!.Value<int>());
        Assert.Equal(5, json["ZoneCount"]!.Value<int>());
        Assert.Equal("Bullish", json["Trend"]!.Value<string>());
    }

    [Fact]
    public async Task GetPairConfig_ReturnsError_WhenNotFound()
    {
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        var result = await ConfigTools.GetPairConfig(_configTableClient, "EUR_USD", "H1");

        var json = JObject.Parse(result);
        Assert.Contains("No config found", json["Error"]!.Value<string>());
    }

    #endregion

    #region UpdatePairConfig

    [Fact]
    public async Task UpdatePairConfig_CreatesNew_WithVersion1()
    {
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        _configsTableMock
            .Setup(x => x.UpsertEntityAsync(It.IsAny<TableEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var result = await ConfigTools.UpdatePairConfig(
            _configTableClient, "EUR_USD", "H1", "H8", true, false);

        var json = JObject.Parse(result);
        Assert.Equal("Updated", json["Status"]!.Value<string>());
        Assert.Equal(1, json["ConfigVersion"]!.Value<int>());

        _configsTableMock.Verify(x => x.UpsertEntityAsync(
            It.Is<TableEntity>(e =>
                e.PartitionKey == "EUR_USD" &&
                e.RowKey == "H1" &&
                e.GetInt32("ConfigVersion") == 1),
            TableUpdateMode.Replace,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePairConfig_IncrementsVersion_WhenExists()
    {
        var existing = CreateConfigEntity(configVersion: 3);
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(existing, Mock.Of<Response>()));

        _configsTableMock
            .Setup(x => x.UpsertEntityAsync(It.IsAny<TableEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var result = await ConfigTools.UpdatePairConfig(
            _configTableClient, "EUR_USD", "H1", "H8", true, false);

        var json = JObject.Parse(result);
        Assert.Equal(4, json["ConfigVersion"]!.Value<int>());
    }

    #endregion

    #region SetPairEnabled

    [Fact]
    public async Task SetPairEnabled_TogglesEnabled_AndIncrementsVersion()
    {
        var existing = CreateConfigEntity(enabled: true, configVersion: 2);
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(existing, Mock.Of<Response>()));

        _configsTableMock
            .Setup(x => x.UpsertEntityAsync(It.IsAny<TableEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var result = await ConfigTools.SetPairEnabled(_configTableClient, "EUR_USD", "H1", false);

        var json = JObject.Parse(result);
        Assert.Equal("Updated", json["Status"]!.Value<string>());
        Assert.False(json["Enabled"]!.Value<bool>());
        Assert.Equal(3, json["ConfigVersion"]!.Value<int>());
    }

    [Fact]
    public async Task SetPairEnabled_ReturnsError_WhenNotFound()
    {
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        var result = await ConfigTools.SetPairEnabled(_configTableClient, "EUR_USD", "H1", true);

        var json = JObject.Parse(result);
        Assert.Contains("No config found", json["Error"]!.Value<string>());
    }

    #endregion

    #region SetPairEmailEnabled

    [Fact]
    public async Task SetPairEmailEnabled_TogglesEmailEnabled()
    {
        var existing = CreateConfigEntity(emailEnabled: false);
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(existing, Mock.Of<Response>()));

        _configsTableMock
            .Setup(x => x.UpsertEntityAsync(It.IsAny<TableEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var result = await ConfigTools.SetPairEmailEnabled(_configTableClient, "EUR_USD", "H1", true);

        var json = JObject.Parse(result);
        Assert.Equal("Updated", json["Status"]!.Value<string>());
        Assert.True(json["EmailEnabled"]!.Value<bool>());
    }

    [Fact]
    public async Task SetPairEmailEnabled_ReturnsError_WhenNotFound()
    {
        _configsTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        var result = await ConfigTools.SetPairEmailEnabled(_configTableClient, "EUR_USD", "H1", true);

        var json = JObject.Parse(result);
        Assert.Contains("No config found", json["Error"]!.Value<string>());
    }

    #endregion

    #region ListPairConfigs

    [Fact]
    public async Task ListPairConfigs_ReturnsAllConfigs()
    {
        var configs = new List<TableEntity>
        {
            CreateConfigEntity("EUR_USD", "H1"),
            CreateConfigEntity("GBP_JPY", "M15", configVersion: 2)
        };

        var statuses = new List<TableEntity>
        {
            CreateStatusEntity("EUR_USD", "H1")
        };

        _configsTableMock
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages(new[]
            {
                Page<TableEntity>.FromValues(configs, null, Mock.Of<Response>())
            }));

        _statusesTableMock
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages(new[]
            {
                Page<TableEntity>.FromValues(statuses, null, Mock.Of<Response>())
            }));

        var result = await ConfigTools.ListPairConfigs(_configTableClient);

        var json = JObject.Parse(result);
        Assert.Equal(2, json["TotalConfigs"]!.Value<int>());
        var configArray = json["Configs"]!.ToObject<JArray>()!;
        Assert.Equal("EUR_USD", configArray[0]["Instrument"]!.Value<string>());
        Assert.Equal("GBP_JPY", configArray[1]["Instrument"]!.Value<string>());
        // First config should have status data, second should not
        Assert.Equal(5, configArray[0]["ZoneCount"]!.Value<int>());
        Assert.Null(configArray[1]["ZoneCount"]!.Value<int?>());
    }

    #endregion

    #region GetPairStatus

    [Fact]
    public async Task GetPairStatus_ReturnsStatus_WhenExists()
    {
        var statusEntity = CreateStatusEntity();
        _statusesTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(statusEntity, Mock.Of<Response>()));

        var result = await ConfigTools.GetPairStatus(_configTableClient, "EUR_USD", "H1");

        var json = JObject.Parse(result);
        Assert.Equal("EUR_USD", json["Instrument"]!.Value<string>());
        Assert.Equal("Bullish", json["Trend"]!.Value<string>());
        Assert.Equal(5, json["ZoneCount"]!.Value<int>());
    }

    [Fact]
    public async Task GetPairStatus_ReturnsError_WhenNotFound()
    {
        _statusesTableMock
            .Setup(x => x.GetEntityIfExistsAsync<TableEntity>("EUR_USD", "H1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<TableEntity>(null!, Mock.Of<Response>()));

        var result = await ConfigTools.GetPairStatus(_configTableClient, "EUR_USD", "H1");

        var json = JObject.Parse(result);
        Assert.Contains("No status found", json["Error"]!.Value<string>());
    }

    #endregion
}
