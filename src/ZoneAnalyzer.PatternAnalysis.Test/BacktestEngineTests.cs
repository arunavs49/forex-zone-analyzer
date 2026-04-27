using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;
using ZoneAnalyzer.PatternAnalysis;
using ZoneAnalyzer.PatternAnalysis.Backtesting;

namespace ZoneAnalyzer.PatternAnalysis.Test;

public class BacktestEngineTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static Candlestick MakeCandle(DateTime time, double open, double high, double low, double close)
    {
        return new Candlestick(
            time: time.ToString("O", CultureInfo.InvariantCulture),
            mid: new CandlestickData(open, high, low, close));
    }

    private static Candlestick MakeCandle(int hourOffset, double open, double high, double low, double close)
    {
        var time = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(hourOffset);
        return MakeCandle(time, open, high, low, close);
    }

    /// <summary>
    /// Creates a zone object directly (bypassing ZoneFinder) for controlled testing.
    /// </summary>
    private static Zone MakeZone(
        ZoneType type,
        DateTime endTime,
        double baseHigh,
        double baseLow,
        int baseCandleCount = 3,
        double legInStart = 0,
        double legInEnd = 0,
        double legOutStart = 0,
        double legOutEnd = 0)
    {
        var baseRange = baseHigh - baseLow;
        return new Zone
        {
            Type = type,
            StartTime = endTime.AddHours(-5),
            EndTime = endTime,
            BaseRangeHigh = baseHigh,
            BaseRangeLow = baseLow,
            BaseCandleCount = baseCandleCount,
            // Leg ratios >= 1× base range so ZoneConfiguration.IsMatch passes
            LegInStartPrice = legInStart != 0 ? legInStart : (type == ZoneType.Demand ? baseHigh + baseRange * 2 : baseLow - baseRange * 2),
            LegInEndPrice = legInEnd != 0 ? legInEnd : (type == ZoneType.Demand ? baseLow : baseHigh),
            LegOutStartPrice = legOutStart != 0 ? legOutStart : (type == ZoneType.Demand ? baseLow : baseHigh),
            LegOutEndPrice = legOutEnd != 0 ? legOutEnd : (type == ZoneType.Demand ? baseHigh + baseRange * 2 : baseLow - baseRange * 2),
        };
    }

    /// <summary>
    /// Generates an uptrend series of candles for trend detection.
    /// Produces swing highs and lows with ascending pattern.
    /// </summary>
    private static List<Candlestick> MakeUptrendCandles(DateTime startTime, int count = 31)
    {
        var mids = new double[]
        {
            1.00, 1.01, 1.02, 1.04, 1.05, 1.04, 1.03, 1.02,
            1.03, 1.04, 1.05, 1.06, 1.07, 1.06, 1.05, 1.04,
            1.05, 1.06, 1.07, 1.08, 1.09, 1.08, 1.07, 1.06,
            1.07, 1.08, 1.09, 1.10, 1.11, 1.10, 1.09,
        };
        return mids.Take(count).Select((m, i) =>
            MakeCandle(startTime.AddHours(i), m, m + 0.0005, m - 0.0005, m)).ToList();
    }

    private static List<Candlestick> MakeDowntrendCandles(DateTime startTime, int count = 31)
    {
        var mids = new double[]
        {
            1.11, 1.10, 1.09, 1.07, 1.06, 1.07, 1.08, 1.09,
            1.08, 1.07, 1.06, 1.05, 1.04, 1.05, 1.06, 1.07,
            1.06, 1.05, 1.04, 1.03, 1.02, 1.03, 1.04, 1.05,
            1.04, 1.03, 1.02, 1.01, 1.00, 1.01, 1.02,
        };
        return mids.Take(count).Select((m, i) =>
            MakeCandle(startTime.AddHours(i), m, m + 0.0005, m - 0.0005, m)).ToList();
    }

    /// <summary>Default permissive zone config that matches any zone with base 1-6 candles.</summary>
    private static ZoneConfiguration DefaultZoneConfig() => new ZoneConfiguration
    {
        MinBaseLength = 1,
        MaxBaseLength = 6,
        MinLegInToBaseRangeRatio = 1,
        MinLegOutToBaseRangeRatio = 1,
    };

    /// <summary>Trend config that works with our 31-candle trend series.</summary>
    private static TrendConfiguration DefaultTrendConfig() => new TrendConfiguration
    {
        SwingLookback = 3,
        TrendCandleCount = 60,
        MinSwingPoints = 2,
    };

    // ── Test 1: Winning demand zone trade ────────────────────────────

    [Fact]
    public void DemandZone_PriceReachesTP_RecordsWin()
    {
        // Zone: base 1.0500–1.0600 (width 0.0100)
        // Entry = baseHigh + spread = 1.0601, SL = baseLow = 1.0500
        // TP = entry + 2×base = 1.0601 + 0.0200 = 1.0801
        var zoneEndTime = new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        var zone = MakeZone(ZoneType.Demand, zoneEndTime, baseHigh: 1.0600, baseLow: 1.0500);

        // Trend candles: uptrend ending before zone formation
        var trendCandles = MakeUptrendCandles(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // Zone candles: price dips to entry then rallies to TP
        var zoneCandles = new List<Candlestick>
        {
            // Before zone — should be ignored
            MakeCandle(zoneEndTime.AddHours(-1), 1.0700, 1.0710, 1.0690, 1.0700),
            // Candle 1: price dips to entry level (L=1.0590 < entry 1.0601)
            MakeCandle(zoneEndTime.AddHours(1), 1.0650, 1.0660, 1.0590, 1.0620),
            // Candle 2: price recovers
            MakeCandle(zoneEndTime.AddHours(2), 1.0620, 1.0700, 1.0610, 1.0690),
            // Candle 3: price hits TP (H=1.0810 >= 1.0801)
            MakeCandle(zoneEndTime.AddHours(3), 1.0690, 1.0810, 1.0680, 1.0800),
        };

        var config = new BacktestConfig
        {
            SpreadAssumption = 0.0001,
            TakeProfitMultiple = 2.0,
            TimeoutCandles = 100,
            MinZonesForScoring = 1,
            FilterByTrend = false, // Don't filter so we test the trade itself
        };
        var engine = new BacktestEngine(config);

        var result = engine.Evaluate(
            new List<Zone> { zone }, zoneCandles, trendCandles,
            DefaultZoneConfig(), DefaultTrendConfig());

        Assert.Equal(1, result.TradedZones);
        Assert.Equal(1, result.Wins);
        Assert.Equal(0, result.Losses);

        var trade = result.Trades[0];
        Assert.Equal(TradeOutcome.Win, trade.Outcome);
        Assert.Equal(1.0601, trade.EntryPrice, 4);
        Assert.Equal(2.0, trade.RiskReward, 1);
    }

    // ── Test 2: Losing supply zone trade ─────────────────────────────

    [Fact]
    public void SupplyZone_PriceBreaksThrough_RecordsLoss()
    {
        // Zone: supply base 1.0800–1.0900 (width 0.0100)
        // Entry = baseLow - spread = 1.0799, SL = baseHigh = 1.0900
        // Price breaks upward through SL
        var zoneEndTime = new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        var zone = MakeZone(ZoneType.Supply, zoneEndTime, baseHigh: 1.0900, baseLow: 1.0800);

        var trendCandles = MakeDowntrendCandles(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var zoneCandles = new List<Candlestick>
        {
            // Before zone
            MakeCandle(zoneEndTime.AddHours(-1), 1.0750, 1.0760, 1.0740, 1.0750),
            // Candle 1: price rises to entry level (H=1.0810 >= entry 1.0799)
            MakeCandle(zoneEndTime.AddHours(1), 1.0760, 1.0810, 1.0750, 1.0800),
            // Candle 2: price breaks through SL (H=1.0910 >= SL 1.0900)
            MakeCandle(zoneEndTime.AddHours(2), 1.0800, 1.0910, 1.0790, 1.0900),
        };

        var config = new BacktestConfig
        {
            SpreadAssumption = 0.0001,
            TakeProfitMultiple = 2.0,
            TimeoutCandles = 100,
            MinZonesForScoring = 1,
            FilterByTrend = false,
        };
        var engine = new BacktestEngine(config);

        var result = engine.Evaluate(
            new List<Zone> { zone }, zoneCandles, trendCandles,
            DefaultZoneConfig(), DefaultTrendConfig());

        Assert.Equal(1, result.Losses);
        Assert.Equal(0, result.Wins);

        var trade = result.Trades[0];
        Assert.Equal(TradeOutcome.Loss, trade.Outcome);
        Assert.Equal(-1.0, trade.RiskReward, 1);
        Assert.Equal(1.0900, trade.ExitPrice);
    }

    // ── Test 3: Timeout scenario ─────────────────────────────────────

    [Fact]
    public void DemandZone_NeitherTPNorSLHit_RecordsTimeout()
    {
        // Zone: demand base 1.0500–1.0600 (width 0.0100)
        // Entry 1.0601, SL 1.0500, TP 1.0801
        // Price enters zone but stays between SL and TP
        var zoneEndTime = new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        var zone = MakeZone(ZoneType.Demand, zoneEndTime, baseHigh: 1.0600, baseLow: 1.0500);

        var trendCandles = MakeUptrendCandles(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // 5 candles after zone: price dips to entry but never reaches TP or SL
        var zoneCandles = new List<Candlestick>
        {
            // Candle that triggers entry (L=1.0590 < 1.0601)
            MakeCandle(zoneEndTime.AddHours(1), 1.0650, 1.0660, 1.0590, 1.0620),
            // Subsequent candles stay in range (above SL=1.0500, below TP=1.0801)
            MakeCandle(zoneEndTime.AddHours(2), 1.0620, 1.0700, 1.0610, 1.0680),
            MakeCandle(zoneEndTime.AddHours(3), 1.0680, 1.0720, 1.0650, 1.0700),
            MakeCandle(zoneEndTime.AddHours(4), 1.0700, 1.0750, 1.0690, 1.0740),
            MakeCandle(zoneEndTime.AddHours(5), 1.0740, 1.0760, 1.0720, 1.0750),
        };

        var config = new BacktestConfig
        {
            SpreadAssumption = 0.0001,
            TakeProfitMultiple = 2.0,
            TimeoutCandles = 5, // Only 5 candles allowed
            MinZonesForScoring = 1,
            FilterByTrend = false,
        };
        var engine = new BacktestEngine(config);

        var result = engine.Evaluate(
            new List<Zone> { zone }, zoneCandles, trendCandles,
            DefaultZoneConfig(), DefaultTrendConfig());

        Assert.Equal(1, result.Timeouts);
        Assert.Equal(0, result.Wins);
        Assert.Equal(0, result.Losses);

        var trade = result.Trades[0];
        Assert.Equal(TradeOutcome.Timeout, trade.Outcome);
    }

    // ── Test 4: No-lookahead verification ────────────────────────────

    [Fact]
    public void TrendEvaluation_OnlyUsesCandles_BeforeZoneFormation()
    {
        // Zone forms at hour 31 of an uptrend — trend candles before that are up.
        // After zone formation we add downtrend candles that should NOT affect trend.
        var trendStart = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var zoneEndTime = trendStart.AddHours(31); // After uptrend candles

        var zone = MakeZone(ZoneType.Demand, zoneEndTime, baseHigh: 1.0600, baseLow: 1.0500);

        // Uptrend candles before zone formation
        var trendCandles = MakeUptrendCandles(trendStart);
        // Add strong downtrend candles AFTER zone formation (these should be ignored)
        for (int i = 32; i < 62; i++)
        {
            trendCandles.Add(MakeCandle(trendStart.AddHours(i),
                1.11 - (i - 31) * 0.01, 1.11 - (i - 31) * 0.01 + 0.0005,
                1.11 - (i - 31) * 0.01 - 0.0005, 1.11 - (i - 31) * 0.01));
        }

        // Zone candles: simple entry + win
        var zoneCandles = new List<Candlestick>
        {
            MakeCandle(zoneEndTime.AddHours(1), 1.0650, 1.0660, 1.0590, 1.0620),
            MakeCandle(zoneEndTime.AddHours(2), 1.0690, 1.0810, 1.0680, 1.0800),
        };

        // FilterByTrend = true so the trade is only taken if trend is Up (with-trend for demand)
        var config = new BacktestConfig
        {
            SpreadAssumption = 0.0001,
            TakeProfitMultiple = 2.0,
            TimeoutCandles = 100,
            MinZonesForScoring = 1,
            FilterByTrend = true,
        };
        var engine = new BacktestEngine(config);

        var result = engine.Evaluate(
            new List<Zone> { zone }, zoneCandles, trendCandles,
            DefaultZoneConfig(), DefaultTrendConfig());

        // If no-lookahead works, trend at formation is Up → trade is taken (not skipped)
        var trade = result.Trades[0];
        Assert.Equal(TrendDirection.Up, trade.TrendAtFormation);
        Assert.True(trade.WithTrend);
        Assert.NotEqual(TradeOutcome.Skipped, trade.Outcome);
    }

    // ── Test 5: Spread correctly applied to entry price ──────────────

    [Theory]
    [InlineData(0.0001)]
    [InlineData(0.0005)]
    [InlineData(0.0010)]
    public void SpreadAssumption_CorrectlyAppliedToEntry_DemandZone(double spread)
    {
        var zoneEndTime = new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        var zone = MakeZone(ZoneType.Demand, zoneEndTime, baseHigh: 1.0600, baseLow: 1.0500);

        var trendCandles = MakeUptrendCandles(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        // Candle that triggers entry
        var zoneCandles = new List<Candlestick>
        {
            MakeCandle(zoneEndTime.AddHours(1), 1.0650, 1.0660, 1.0500, 1.0620),
        };

        var config = new BacktestConfig
        {
            SpreadAssumption = spread,
            TakeProfitMultiple = 2.0,
            TimeoutCandles = 100,
            MinZonesForScoring = 1,
            FilterByTrend = false,
        };
        var engine = new BacktestEngine(config);

        var result = engine.Evaluate(
            new List<Zone> { zone }, zoneCandles, trendCandles,
            DefaultZoneConfig(), DefaultTrendConfig());

        var trade = result.Trades[0];
        // Demand entry = baseHigh + spread
        Assert.Equal(1.0600 + spread, trade.EntryPrice, 5);
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(0.0005)]
    public void SpreadAssumption_CorrectlyAppliedToEntry_SupplyZone(double spread)
    {
        var zoneEndTime = new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        var zone = MakeZone(ZoneType.Supply, zoneEndTime, baseHigh: 1.0900, baseLow: 1.0800);

        var trendCandles = MakeDowntrendCandles(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var zoneCandles = new List<Candlestick>
        {
            MakeCandle(zoneEndTime.AddHours(1), 1.0760, 1.0810, 1.0750, 1.0800),
        };

        var config = new BacktestConfig
        {
            SpreadAssumption = spread,
            TakeProfitMultiple = 2.0,
            TimeoutCandles = 100,
            MinZonesForScoring = 1,
            FilterByTrend = false,
        };
        var engine = new BacktestEngine(config);

        var result = engine.Evaluate(
            new List<Zone> { zone }, zoneCandles, trendCandles,
            DefaultZoneConfig(), DefaultTrendConfig());

        var trade = result.Trades[0];
        // Supply entry = baseLow - spread
        Assert.Equal(1.0800 - spread, trade.EntryPrice, 5);
    }

    // ── Test 6: Empty zones list returns empty results ────────────────

    [Fact]
    public void EmptyZonesList_ReturnsEmptyResult()
    {
        var trendCandles = MakeUptrendCandles(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var zoneCandles = new List<Candlestick>
        {
            MakeCandle(1, 1.0500, 1.0510, 1.0490, 1.0500),
        };

        var config = new BacktestConfig { MinZonesForScoring = 1, FilterByTrend = false };
        var engine = new BacktestEngine(config);

        var result = engine.Evaluate(
            new List<Zone>(), zoneCandles, trendCandles,
            DefaultZoneConfig(), DefaultTrendConfig());

        Assert.Equal(0, result.TotalZones);
        Assert.Equal(0, result.MatchedZones);
        Assert.Equal(0, result.TradedZones);
        Assert.Equal(0, result.Wins);
        Assert.Equal(0, result.Losses);
        Assert.Equal(0, result.Timeouts);
        Assert.Empty(result.Trades);
        Assert.Equal(0, result.Score);
    }
}
