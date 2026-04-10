using System.ComponentModel;
using System.Text.Json;
using ForexZoneAnalyzer.McpServer.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.McpServer.Tools;

[McpServerToolType]
public sealed class AccountTools
{
    [McpServerTool(Name = "list_accounts"), Description("List all OANDA trading accounts available for this connection.")]
    public static async Task<string> ListAccounts(
        IOandaConnectionService connectionService,
        CancellationToken cancellationToken)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var accounts = connection.GetAccounts();
        var result = accounts.Select(a => new { a.Id, a.Tags }).ToList();
        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "get_account_summary"), Description("Get account summary including balance, P&L, margin used, open trade/position/order counts.")]
    public static async Task<string> GetAccountSummary(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID (e.g. '101-001-12345678-001')")] string accountId,
        CancellationToken cancellationToken)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var account = connection.GetAccount(accountId);
        var summary = await account.GetSummaryAsync();
        return JsonConvert.SerializeObject(summary, Formatting.Indented);
    }

    [McpServerTool(Name = "get_account_details"), Description("Get full account details including all positions, orders, and trades.")]
    public static async Task<string> GetAccountDetails(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID (e.g. '101-001-12345678-001')")] string accountId,
        CancellationToken cancellationToken)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var account = connection.GetAccount(accountId);
        var details = await account.GetDetailsAsync();
        return JsonConvert.SerializeObject(details, Formatting.Indented);
    }

    [McpServerTool(Name = "get_tradeable_instruments"), Description("List all instruments (currency pairs) available for trading on an account.")]
    public static async Task<string> GetTradeableInstruments(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        CancellationToken cancellationToken)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var account = connection.GetAccount(accountId);
        var instruments = await account.GetTradeableInstrumentsAsync();
        var result = instruments.Select(i => new { i.Name, i.Type, i.DisplayName }).ToList();
        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }
}
