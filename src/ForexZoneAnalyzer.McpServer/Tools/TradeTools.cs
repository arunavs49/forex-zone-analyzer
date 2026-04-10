using System.ComponentModel;
using ForexZoneAnalyzer.McpServer.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Trade;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.McpServer.Tools;

[McpServerToolType]
public sealed class TradeTools
{
    [McpServerTool(Name = "get_open_trades"), Description("List all currently open trades for an account.")]
    public static async Task<string> GetOpenTrades(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        CancellationToken cancellationToken)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var account = connection.GetAccount(accountId);
        var trades = await account.Trades.GetOpenTradesAsync();

        var result = trades.Select(t => new
        {
            t.Id,
            t.Instrument,
            t.CurrentUnits,
            t.Price,
            t.UnrealizedPL,
            t.RealizedPL,
            t.State,
            t.OpenTime
        }).ToList();

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "open_trade"), Description("Open a new market order trade for a forex instrument. WARNING: This creates a real trade with real money.")]
    public static async Task<string> OpenTrade(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Trade direction: 'Long' (buy) or 'Short' (sell)")] string direction,
        [Description("Number of units to trade")] long units,
        [Description("Trailing stop loss distance in pips")] int trailingStopLossPips,
        CancellationToken cancellationToken)
    {
        var instrumentName = Enum.Parse<InstrumentName>(instrument, ignoreCase: true);
        var tradeDirection = Enum.Parse<TradeDirection>(direction, ignoreCase: true);

        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var account = connection.GetAccount(accountId);
        var response = await account.Trades.OpenTradeAsync(
            instrumentName, tradeDirection, units, trailingStopLossPips);

        return JsonConvert.SerializeObject(response, Formatting.Indented);
    }

    [McpServerTool(Name = "close_trade"), Description("Close an existing open trade. WARNING: This closes a real trade.")]
    public static async Task<string> CloseTrade(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        [Description("The trade ID to close")] string tradeId,
        [Description("Number of units to close (omit or 'ALL' to close entire position)")] string units = "ALL",
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);

        var body = new CloseTradeRequest(units: units);
        var response = connection.TradeApi.CloseTrade(accountId, tradeId, body);

        return JsonConvert.SerializeObject(response, Formatting.Indented);
    }
}
