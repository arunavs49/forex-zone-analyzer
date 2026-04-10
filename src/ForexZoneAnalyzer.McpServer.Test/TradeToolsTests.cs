using ForexZoneAnalyzer.McpServer.Services;
using ForexZoneAnalyzer.McpServer.Tools;
using GeriRemenyi.Oanda.V20.Client.Api;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk;
using GeriRemenyi.Oanda.V20.Sdk.Account;
using GeriRemenyi.Oanda.V20.Sdk.Trade;
using Moq;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.McpServer.Test;

public class TradeToolsTests
{
    private readonly Mock<IOandaConnectionService> _connectionServiceMock;
    private readonly Mock<IOandaApiConnection> _connectionMock;
    private readonly Mock<IAccount> _accountMock;
    private readonly Mock<ITrades> _tradesMock;
    private readonly Mock<ITradeApi> _tradeApiMock;

    public TradeToolsTests()
    {
        _connectionServiceMock = new Mock<IOandaConnectionService>();
        _connectionMock = new Mock<IOandaApiConnection>();
        _accountMock = new Mock<IAccount>();
        _tradesMock = new Mock<ITrades>();
        _tradeApiMock = new Mock<ITradeApi>();

        _connectionServiceMock
            .Setup(x => x.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_connectionMock.Object);

        _connectionMock
            .Setup(x => x.GetAccount(It.IsAny<string>()))
            .Returns(_accountMock.Object);

        _accountMock.Setup(x => x.Trades).Returns(_tradesMock.Object);
        _connectionMock.Setup(x => x.TradeApi).Returns(_tradeApiMock.Object);
    }

    [Fact]
    public async Task GetOpenTrades_ReturnsTradeList()
    {
        var trades = new List<Trade>
        {
            new Trade
            {
                Id = 12345,
                Instrument = InstrumentName.EUR_USD,
                CurrentUnits = 1000,
                Price = 1.1050,
                UnrealizedPL = 25.50,
                State = TradeState.OPEN,
                OpenTime = "2025-01-15T10:30:00.000000000Z"
            }
        };
        _tradesMock.Setup(x => x.GetOpenTradesAsync()).ReturnsAsync(trades);

        var result = await TradeTools.GetOpenTrades(
            _connectionServiceMock.Object, "test-account", CancellationToken.None);

        Assert.Contains("12345", result);
        Assert.Contains("EUR_USD", result);
        Assert.Contains("1000", result);
    }

    [Fact]
    public async Task GetOpenTrades_EmptyList_ReturnsEmptyArray()
    {
        _tradesMock.Setup(x => x.GetOpenTradesAsync()).ReturnsAsync(new List<Trade>());

        var result = await TradeTools.GetOpenTrades(
            _connectionServiceMock.Object, "test-account", CancellationToken.None);

        Assert.Equal("[]", result.Trim());
    }

    [Fact]
    public async Task OpenTrade_CallsWithCorrectParameters()
    {
        var response = new CreateOrderResponse();
        _tradesMock
            .Setup(x => x.OpenTradeAsync(
                InstrumentName.EUR_USD,
                TradeDirection.Long,
                1000,
                20))
            .ReturnsAsync(response);

        var result = await TradeTools.OpenTrade(
            _connectionServiceMock.Object,
            "test-account",
            "EUR_USD",
            "Long",
            1000,
            20,
            CancellationToken.None);

        _tradesMock.Verify(x => x.OpenTradeAsync(
            InstrumentName.EUR_USD,
            TradeDirection.Long,
            1000,
            20), Times.Once);
    }

    [Fact]
    public async Task OpenTrade_ShortDirection_ParsesCorrectly()
    {
        var response = new CreateOrderResponse();
        _tradesMock
            .Setup(x => x.OpenTradeAsync(
                It.IsAny<InstrumentName>(),
                TradeDirection.Short,
                It.IsAny<long>(),
                It.IsAny<int>()))
            .ReturnsAsync(response);

        await TradeTools.OpenTrade(
            _connectionServiceMock.Object,
            "test-account",
            "GBP_JPY",
            "Short",
            500,
            15,
            CancellationToken.None);

        _tradesMock.Verify(x => x.OpenTradeAsync(
            InstrumentName.GBP_JPY,
            TradeDirection.Short,
            500,
            15), Times.Once);
    }

    [Fact]
    public async Task CloseTrade_CallsTradeApiWithCorrectParams()
    {
        var response = new CloseTradeResponse();
        _tradeApiMock
            .Setup(x => x.CloseTrade(
                "test-account",
                "12345",
                It.IsAny<CloseTradeRequest>()))
            .Returns(response);

        var result = await TradeTools.CloseTrade(
            _connectionServiceMock.Object,
            "test-account",
            "12345",
            "ALL");

        _tradeApiMock.Verify(x => x.CloseTrade(
            "test-account",
            "12345",
            It.Is<CloseTradeRequest>(r => r.Units == "ALL")), Times.Once);
    }

    [Fact]
    public async Task CloseTrade_PartialClose_PassesUnits()
    {
        var response = new CloseTradeResponse();
        _tradeApiMock
            .Setup(x => x.CloseTrade(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CloseTradeRequest>()))
            .Returns(response);

        await TradeTools.CloseTrade(
            _connectionServiceMock.Object,
            "test-account",
            "12345",
            "500");

        _tradeApiMock.Verify(x => x.CloseTrade(
            "test-account",
            "12345",
            It.Is<CloseTradeRequest>(r => r.Units == "500")), Times.Once);
    }
}
