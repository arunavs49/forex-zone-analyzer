using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;
using ZoneAnalyzer.PatternAnalysis;

namespace ZoneAnalyzer.PatternAnalysis.Test;

public class TrendManagerTests
{
    private static Candlestick MakeCandle(int index, double open, double high, double low, double close)
    {
        return new Candlestick
        {
            Time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddHours(index)
                .ToString("O", CultureInfo.InvariantCulture),
            Mid = new CandlestickData { O = open, H = high, L = low, C = close }
        };
    }

    private static List<Candlestick> FromMids(double[] mids)
    {
        return mids.Select((m, i) => MakeCandle(i, m, m + 0.0005, m - 0.0005, m)).ToList();
    }

    private static List<Candlestick> MakeUptrend()
    {
        var mids = new double[]
        {
            1.00, 1.01, 1.02, 1.04, 1.05, 1.04, 1.03, 1.02,
            1.03, 1.04, 1.05, 1.06, 1.07, 1.06, 1.05, 1.04,
            1.05, 1.06, 1.07, 1.08, 1.09, 1.08, 1.07, 1.06,
            1.07, 1.08, 1.09, 1.10, 1.11, 1.10, 1.09,
        };
        return FromMids(mids);
    }

    private static List<Candlestick> MakeDowntrend()
    {
        var mids = new double[]
        {
            1.11, 1.10, 1.09, 1.07, 1.06, 1.07, 1.08, 1.09,
            1.08, 1.07, 1.06, 1.05, 1.04, 1.05, 1.06, 1.07,
            1.06, 1.05, 1.04, 1.03, 1.02, 1.03, 1.04, 1.05,
            1.04, 1.03, 1.02, 1.01, 1.00, 1.01, 1.02,
        };
        return FromMids(mids);
    }

    private static List<Candlestick> MakeSideways()
    {
        var mids = new double[]
        {
            1.00, 1.01, 1.02, 1.04, 1.05, 1.04, 1.03, 1.02,
            1.03, 1.04, 1.05, 1.04, 1.03, 1.02,
            1.03, 1.04, 1.05, 1.04, 1.03, 1.02, 1.03, 1.04,
        };
        return FromMids(mids);
    }

    [Fact]
    public void Uptrend_DetectedAsUp()
    {
        var candles = MakeUptrend();
        var manager = TrendManager.Create(candles);
        Assert.Equal(TrendDirection.Up, manager.GetTrendDirection());
    }

    [Fact]
    public void Downtrend_DetectedAsDown()
    {
        var candles = MakeDowntrend();
        var manager = TrendManager.Create(candles);
        Assert.Equal(TrendDirection.Down, manager.GetTrendDirection());
    }

    [Fact]
    public void Sideways_DetectedAsSideways()
    {
        var candles = MakeSideways();
        var manager = TrendManager.Create(candles);
        Assert.Equal(TrendDirection.Sideways, manager.GetTrendDirection());
    }

    [Fact]
    public void GetTrend_ReturnsStringMatchingDirection()
    {
        var candles = MakeUptrend();
        var manager = TrendManager.Create(candles);
        Assert.Equal(manager.GetTrendDirection().ToString(), manager.GetTrend());
    }

    [Fact]
    public void FindSwingHighs_IdentifiesLocalMaxima()
    {
        var candles = new List<Candlestick>
        {
            MakeCandle(0, 1.0, 1.01, 0.99, 1.0),
            MakeCandle(1, 1.0, 1.02, 0.99, 1.0),
            MakeCandle(2, 1.0, 1.05, 0.99, 1.0),
            MakeCandle(3, 1.0, 1.02, 0.99, 1.0),
            MakeCandle(4, 1.0, 1.01, 0.99, 1.0),
        };

        var highs = TrendManager.FindSwingHighs(candles, 2);
        Assert.Single(highs);
        Assert.Equal(2, highs[0].Index);
        Assert.Equal(1.05, highs[0].Price);
    }

    [Fact]
    public void FindSwingLows_IdentifiesLocalMinima()
    {
        var candles = new List<Candlestick>
        {
            MakeCandle(0, 1.0, 1.01, 1.00, 1.0),
            MakeCandle(1, 1.0, 1.01, 0.98, 1.0),
            MakeCandle(2, 1.0, 1.01, 0.95, 1.0),
            MakeCandle(3, 1.0, 1.01, 0.98, 1.0),
            MakeCandle(4, 1.0, 1.01, 1.00, 1.0),
        };

        var lows = TrendManager.FindSwingLows(candles, 2);
        Assert.Single(lows);
        Assert.Equal(2, lows[0].Index);
        Assert.Equal(0.95, lows[0].Price);
    }

    [Fact]
    public void FindSwingHighs_MultipleSwingPoints()
    {
        var mids = new double[]
        {
            1.00, 1.01, 1.02, 1.04, 1.05, 1.04, 1.03, 1.02,
            1.03, 1.04, 1.05, 1.07, 1.08, 1.07, 1.06, 1.05, 1.04, 1.03,
        };
        var candles = FromMids(mids);

        var highs = TrendManager.FindSwingHighs(candles, 3);
        Assert.Equal(2, highs.Count);
        Assert.Equal(4, highs[0].Index);
        Assert.Equal(12, highs[1].Index);
    }

    [Fact]
    public void FlatCandles_ProducesSidewaysTrend()
    {
        var candles = Enumerable.Range(0, 20)
            .Select(i => MakeCandle(i, 1.0, 1.01, 0.99, 1.0))
            .ToList();

        var manager = TrendManager.Create(candles);
        Assert.Equal(TrendDirection.Sideways, manager.GetTrendDirection());
    }

    [Fact]
    public void SwingLookback_AffectsSwingPointCount()
    {
        var candles = MakeUptrend();

        var configTight = new TrendConfiguration { SwingLookback = 2 };
        var configWide = new TrendConfiguration { SwingLookback = 5 };

        var managerTight = TrendManager.Create(candles, configTight);
        var managerWide = TrendManager.Create(candles, configWide);

        Assert.True(managerTight.GetSwingHighs().Count >= managerWide.GetSwingHighs().Count);
    }

    [Fact]
    public void TooFewCandles_ReturnsSideways()
    {
        var candles = Enumerable.Range(0, 5)
            .Select(i => MakeCandle(i, 1.0 + i * 0.01, 1.01 + i * 0.01, 0.99 + i * 0.01, 1.0 + i * 0.01))
            .ToList();

        var manager = TrendManager.Create(candles);
        Assert.Equal(TrendDirection.Sideways, manager.GetTrendDirection());
    }

    [Fact]
    public void MonotonicRise_ReturnsSideways()
    {
        var candles = Enumerable.Range(0, 20)
            .Select(i => MakeCandle(i, 1.0 + i * 0.001, 1.001 + i * 0.001, 0.999 + i * 0.001, 1.0 + i * 0.001))
            .ToList();

        var manager = TrendManager.Create(candles);
        Assert.Equal(TrendDirection.Sideways, manager.GetTrendDirection());
    }

    [Fact]
    public void DefaultConfig_HasExpectedValues()
    {
        var config = new TrendConfiguration();
        Assert.Equal(3, config.SwingLookback);
        Assert.Equal(60, config.TrendCandleCount);
        Assert.Equal(2, config.MinSwingPoints);
    }

    [Fact]
    public void CustomConfig_DoesNotCrash()
    {
        var config = new TrendConfiguration
        {
            SwingLookback = 5,
            TrendCandleCount = 100,
            MinSwingPoints = 3
        };

        var candles = MakeUptrend();
        var manager = TrendManager.Create(candles, config);
        var result = manager.GetTrendDirection();
        Assert.True(result == TrendDirection.Up || result == TrendDirection.Down || result == TrendDirection.Sideways);
    }

    [Fact]
    public void HighMinSwingPoints_ReturnsSideways()
    {
        var config = new TrendConfiguration { MinSwingPoints = 10 };
        var candles = MakeUptrend();
        var manager = TrendManager.Create(candles, config);
        Assert.Equal(TrendDirection.Sideways, manager.GetTrendDirection());
    }

    [Fact]
    public void Create_WithoutConfig_UsesDefaults()
    {
        var candles = MakeUptrend();
        var manager = TrendManager.Create(candles);
        var trend = manager.GetTrend();
        Assert.Contains(trend, new[] { "Up", "Down", "Sideways" });
    }

    [Fact]
    public void GetSwingHighs_AscendingInUptrend()
    {
        var candles = MakeUptrend();
        var manager = TrendManager.Create(candles);
        var highs = manager.GetSwingHighs();
        Assert.True(highs.Count >= 2);
        for (int i = 1; i < highs.Count; i++)
            Assert.True(highs[i].Price > highs[i - 1].Price);
    }

    [Fact]
    public void GetSwingLows_AscendingInUptrend()
    {
        var candles = MakeUptrend();
        var manager = TrendManager.Create(candles);
        var lows = manager.GetSwingLows();
        Assert.True(lows.Count >= 2);
        for (int i = 1; i < lows.Count; i++)
            Assert.True(lows[i].Price > lows[i - 1].Price);
    }
}
