using ForexZoneAnalyzer.McpServer.Services;
using ForexZoneAnalyzer.McpServer.Tools;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk;
using GeriRemenyi.Oanda.V20.Sdk.Account;
using Moq;
using Newtonsoft.Json;
using ClientModel = GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.McpServer.Test;

public class AccountToolsTests
{
    private readonly Mock<IOandaConnectionService> _connectionServiceMock;
    private readonly Mock<IOandaApiConnection> _connectionMock;
    private readonly Mock<IAccount> _accountMock;

    public AccountToolsTests()
    {
        _connectionServiceMock = new Mock<IOandaConnectionService>();
        _connectionMock = new Mock<IOandaApiConnection>();
        _accountMock = new Mock<IAccount>();

        _connectionServiceMock
            .Setup(x => x.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_connectionMock.Object);

        _connectionMock
            .Setup(x => x.GetAccount(It.IsAny<string>()))
            .Returns(_accountMock.Object);
    }

    [Fact]
    public async Task ListAccounts_ReturnsAccountList()
    {
        var accounts = new List<AccountProperties>
        {
            new AccountProperties { Id = "101-001-12345678-001" },
            new AccountProperties { Id = "101-001-12345678-002" }
        };
        _connectionMock.Setup(x => x.GetAccounts()).Returns(accounts);

        var result = await AccountTools.ListAccounts(_connectionServiceMock.Object, CancellationToken.None);

        Assert.Contains("101-001-12345678-001", result);
        Assert.Contains("101-001-12345678-002", result);
    }

    [Fact]
    public async Task GetAccountSummary_ReturnsSummary()
    {
        var summary = new AccountSummary { Balance = 10000.0, UnrealizedPL = 250.0 };
        _accountMock.Setup(x => x.GetSummaryAsync()).ReturnsAsync(summary);

        var result = await AccountTools.GetAccountSummary(
            _connectionServiceMock.Object, "test-account", CancellationToken.None);

        Assert.Contains("10000", result);
        Assert.Contains("250", result);
    }

    [Fact]
    public async Task GetAccountDetails_ReturnsDetails()
    {
        var details = new ClientModel.Account { Balance = 50000.0, Currency = AccountCurrency.USD };
        _accountMock.Setup(x => x.GetDetailsAsync()).ReturnsAsync(details);

        var result = await AccountTools.GetAccountDetails(
            _connectionServiceMock.Object, "test-account", CancellationToken.None);

        Assert.Contains("50000", result);
        Assert.Contains("USD", result);
    }

    [Fact]
    public async Task GetTradeableInstruments_ReturnsList()
    {
        var instruments = new List<ClientModel.Instrument>
        {
            new ClientModel.Instrument { Name = InstrumentName.EUR_USD, DisplayName = "EUR/USD", Type = InstrumentType.CURRENCY },
            new ClientModel.Instrument { Name = InstrumentName.GBP_JPY, DisplayName = "GBP/JPY", Type = InstrumentType.CURRENCY }
        };
        _accountMock.Setup(x => x.GetTradeableInstrumentsAsync()).ReturnsAsync((IEnumerable<ClientModel.Instrument>)instruments);

        var result = await AccountTools.GetTradeableInstruments(
            _connectionServiceMock.Object, "test-account", CancellationToken.None);

        Assert.Contains("EUR_USD", result);
        Assert.Contains("GBP_JPY", result);
    }
}
