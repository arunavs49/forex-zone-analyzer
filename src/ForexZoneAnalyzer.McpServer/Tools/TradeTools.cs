using System.ComponentModel;
using ForexZoneAnalyzer.McpServer.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Trade;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    [McpServerTool(Name = "place_limit_order"), Description("Place a limit order at a specific entry price with fixed stop loss and take profit. WARNING: This creates a real pending order with real money.")]
    public static async Task<string> PlaceLimitOrder(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        [Description("Instrument name (e.g. 'EUR_USD', 'GBP_JPY')")] string instrument,
        [Description("Trade direction: 'Long' (buy) or 'Short' (sell)")] string direction,
        [Description("Limit entry price at which the order will be filled")] double entryPrice,
        [Description("Stop loss distance in pips from the entry price")] int stopLossPips,
        [Description("Take profit distance in pips from the entry price")] int takeProfitPips,
        [Description("Number of units to trade (position size)")] long units,
        CancellationToken cancellationToken)
    {
        var instrumentName = Enum.Parse<InstrumentName>(instrument, ignoreCase: true);
        var tradeDirection = Enum.Parse<TradeDirection>(direction, ignoreCase: true);

        var pipSize = ResolveInstrumentPipSize(instrumentName);
        var directionMultiplier = tradeDirection == TradeDirection.Long ? 1 : -1;
        var signedUnits = units * directionMultiplier;

        var stopLossPrice = Math.Round(entryPrice - directionMultiplier * stopLossPips * pipSize, 5);
        var takeProfitPrice = Math.Round(entryPrice + directionMultiplier * takeProfitPips * pipSize, 5);

        var stopLoss = new StopLossDetails(price: stopLossPrice);
        var takeProfit = new TakeProfitDetails(price: takeProfitPrice);

        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var response = await connection.OrderApi.CreateOrderAsync(accountId, new CreateOrderRequest(new
        {
            Type = "LIMIT",
            Instrument = instrumentName.ToString(),
            Units = signedUnits.ToString(),
            Price = entryPrice.ToString("F5", System.Globalization.CultureInfo.InvariantCulture),
            StopLossOnFill = new { Price = stopLossPrice.ToString("F5", System.Globalization.CultureInfo.InvariantCulture) },
            TakeProfitOnFill = new { Price = takeProfitPrice.ToString("F5", System.Globalization.CultureInfo.InvariantCulture) }
        }));

        return JsonConvert.SerializeObject(response, Formatting.Indented);
    }

    private static double ResolveInstrumentPipSize(InstrumentName instrument)
    {
        return instrument switch
        {
            InstrumentName.USD_JPY or InstrumentName.EUR_JPY or InstrumentName.GBP_JPY
                or InstrumentName.AUD_JPY or InstrumentName.CAD_JPY or InstrumentName.CHF_JPY
                or InstrumentName.NZD_JPY => 0.01,
            _ => 0.0001
        };
    }

    [McpServerTool(Name = "get_pending_orders"), Description("List all pending (unfilled) orders for an account, including limit and stop orders waiting to be triggered.")]
    public static async Task<string> GetPendingOrders(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        CancellationToken cancellationToken)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        // Use WithHttpInfo to get the raw JSON from OANDA, which preserves all subtype fields
        // (instrument, price, units, stopLossOnFill, takeProfitOnFill, etc.) that are lost
        // when deserialising into the base List<Order> type.
        var response = await connection.OrderApi.GetPendingOrdersAsyncWithHttpInfo(accountId);
        var raw = response?.RawContent;

        if (string.IsNullOrEmpty(raw)) return "[]";

        // OANDA returns { "orders": [...], "lastTransactionID": "..." }
        // Handle both the envelope object and a bare array (e.g. from mocks)
        var token = JToken.Parse(raw);
        return token switch
        {
            JArray arr => arr.ToString(Formatting.Indented),
            JObject obj => (obj["orders"] ?? new JArray()).ToString(Formatting.Indented),
            _ => "[]"
        };
    }

    [McpServerTool(Name = "get_orders"), Description("List all orders for an account. Optionally filter by state: 'PENDING', 'FILLED', 'TRIGGERED', 'CANCELLED'. Leave state empty to return all orders.")]
    public static async Task<string> GetOrders(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        [Description("Optional order state filter: 'PENDING', 'FILLED', 'TRIGGERED', 'CANCELLED'. Leave empty for all.")] string? state = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var response = await connection.OrderApi.GetOrdersAsync(accountId, state: string.IsNullOrWhiteSpace(state) ? null : state);

        return JsonConvert.SerializeObject(response?.Orders ?? [], Formatting.Indented);
    }

    [McpServerTool(Name = "cancel_order"), Description("Cancel a pending order by its order ID. WARNING: This cancels a real pending order.")]
    public static async Task<string> CancelOrder(
        IOandaConnectionService connectionService,
        [Description("The OANDA account ID")] string accountId,
        [Description("The order ID to cancel")] string orderId,
        CancellationToken cancellationToken)
    {
        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var response = await connection.OrderApi.CancelOrderAsync(accountId, orderId);

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
