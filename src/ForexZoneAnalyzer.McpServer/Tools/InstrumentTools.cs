using System.ComponentModel;
using System.Globalization;
using ForexZoneAnalyzer.McpServer.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using ZoneAnalyzer.PatternAnalysis;

namespace ForexZoneAnalyzer.McpServer.Tools;

[McpServerToolType]
public sealed class InstrumentTools
{
    [McpServerTool(Name = "get_candles"), Description("Get candlestick (OHLC) data for a forex instrument over a time range.")]
    public static async Task<string> GetCandles(
        IOandaConnectionService connectionService,
        [Description("Instrument name (e.g. 'EUR_USD', 'GBP_JPY', 'USD_CAD')")] string instrument,
        [Description("Candle granularity: S5, S10, S15, S30, M1, M2, M4, M5, M10, M15, M30, H1, H2, H3, H4, H6, H8, H12, D, W, M")] string granularity,
        [Description("Number of candles to retrieve (max 5000, default 100)")] int count = 100,
        CancellationToken cancellationToken = default)
    {
        var instrumentName = Enum.Parse<InstrumentName>(instrument, ignoreCase: true);
        var gran = Enum.Parse<CandlestickGranularity>(granularity, ignoreCase: true);
        count = Math.Clamp(count, 1, 5000);

        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var inst = connection.GetInstrument(instrumentName);
        var candles = await inst.GetLastNCandlesAsync(gran, count, new[] { PricingComponent.Mid });

        var result = candles.Select(c => new
        {
            Time = c.Time,
            Open = c.Mid?.O,
            High = c.Mid?.H,
            Low = c.Mid?.L,
            Close = c.Mid?.C,
            Volume = c.Volume,
            Complete = c.Complete
        }).ToList();

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "get_candles_by_time"), Description("Get candlestick data for a forex instrument between specific dates.")]
    public static async Task<string> GetCandlesByTime(
        IOandaConnectionService connectionService,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Candle granularity: S5, S10, S15, S30, M1, M2, M4, M5, M10, M15, M30, H1, H2, H3, H4, H6, H8, H12, D, W, M")] string granularity,
        [Description("Start date/time in ISO 8601 format (e.g. '2025-01-01T00:00:00Z')")] string from,
        [Description("End date/time in ISO 8601 format (e.g. '2025-03-01T00:00:00Z')")] string to,
        CancellationToken cancellationToken = default)
    {
        var instrumentName = Enum.Parse<InstrumentName>(instrument, ignoreCase: true);
        var gran = Enum.Parse<CandlestickGranularity>(granularity, ignoreCase: true);
        var fromDate = DateTime.Parse(from, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        var toDate = DateTime.Parse(to, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var inst = connection.GetInstrument(instrumentName);
        var candles = await inst.GetCandlesByTimeAsync(gran, fromDate, toDate, new[] { PricingComponent.Mid });

        var result = candles.Select(c => new
        {
            Time = c.Time,
            Open = c.Mid?.O,
            High = c.Mid?.H,
            Low = c.Mid?.L,
            Close = c.Mid?.C,
            Volume = c.Volume,
            Complete = c.Complete
        }).ToList();

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "get_supply_demand_zones"), Description("Analyze candlestick data to detect supply and demand zones for a forex instrument. Zones include freshness (Untested/Tested/Broken) and whether they worked.")]
    public static async Task<string> GetSupplyDemandZones(
        IOandaConnectionService connectionService,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Candle granularity (e.g. 'H1', 'H4', 'D')")] string granularity,
        [Description("Number of candles to analyze (default 500)")] int count = 500,
        CancellationToken cancellationToken = default)
    {
        var instrumentName = Enum.Parse<InstrumentName>(instrument, ignoreCase: true);
        var gran = Enum.Parse<CandlestickGranularity>(granularity, ignoreCase: true);
        count = Math.Clamp(count, 10, 5000);

        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var inst = connection.GetInstrument(instrumentName);
        var candles = await inst.GetLastNCandlesAsync(gran, count, new[] { PricingComponent.Mid });

        var zoneManager = ZoneManager.Create(candles, new ZoneConfiguration());
        var supplyZones = zoneManager.GetSupplyZones();
        var demandZones = zoneManager.GetDemandZones();

        var result = new
        {
            Instrument = instrument,
            Granularity = granularity,
            CandlesAnalyzed = candles.Count(),
            TotalZones = supplyZones.Count + demandZones.Count,
            SupplyZones = supplyZones.Select(FormatZone).ToList(),
            DemandZones = demandZones.Select(FormatZone).ToList()
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "get_trend"), Description("Determine the current price trend (Up/Down/Sideways) for a forex instrument using linear regression on closing prices.")]
    public static async Task<string> GetTrend(
        IOandaConnectionService connectionService,
        [Description("Instrument name (e.g. 'EUR_USD')")] string instrument,
        [Description("Candle granularity (e.g. 'H1', 'H4', 'D')")] string granularity,
        [Description("Number of candles for trend analysis (default 100, uses last 60 for regression)")] int count = 100,
        CancellationToken cancellationToken = default)
    {
        var instrumentName = Enum.Parse<InstrumentName>(instrument, ignoreCase: true);
        var gran = Enum.Parse<CandlestickGranularity>(granularity, ignoreCase: true);
        count = Math.Clamp(count, 10, 5000);

        var connection = await connectionService.GetConnectionAsync(cancellationToken);
        var inst = connection.GetInstrument(instrumentName);
        var candles = await inst.GetLastNCandlesAsync(gran, count, new[] { PricingComponent.Mid });

        var trendManager = TrendManager.Create(candles);
        var trend = trendManager.GetTrend();

        var result = new
        {
            Instrument = instrument,
            Granularity = granularity,
            Trend = trend,
            CandlesAnalyzed = candles.Count()
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    private static object FormatZone(Zone z) => new
    {
        Type = z.Type.ToString(),
        Freshness = z.Freshness.ToString(),
        Worked = z.Worked,
        SubZone = z.SubZone,
        BaseRangeHigh = z.BaseRangeHigh,
        BaseRangeLow = z.BaseRangeLow,
        BaseCandleCount = z.BaseCandleCount,
        StartTime = z.StartTime.ToString("o"),
        EndTime = z.EndTime.ToString("o")
    };
}
