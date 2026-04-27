using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;
using ZoneAnalyzer.PatternAnalysis;
using ZoneAnalyzer.PatternAnalysis.Backtesting;

namespace ZoneAnalyzer.PatternAnalysis.Test;

public class StrategyOptimizerTests
{
    // ──────────────────────── helpers ────────────────────────

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

    /// <summary>
    /// Build a candle sequence that produces demand zones via the Rally-Base-Rally pattern.
    /// An "ExcitingRally" candle has |O - C| * 2 >= range and C > O.
    /// A "Boring" candle has |O - C| * 2 < range (small body, big wicks).
    /// The pattern repeats to generate multiple zones.
    /// After zones form, add trailing candles so backtesting can evaluate trades.
    /// </summary>
    private static List<Candlestick> BuildZoneProducingCandles(int zoneCount = 15)
    {
        var candles = new List<Candlestick>();
        int idx = 0;
        double price = 1.1000;

        for (int z = 0; z < zoneCount; z++)
        {
            // Leg-in: 2 exciting rally candles (strong upward move)
            for (int i = 0; i < 2; i++)
            {
                double o = price;
                double c = price + 0.0050;
                // Rally: C > O, body = 0.0050, range = 0.0055, body*2=0.01 >= 0.0055
                candles.Add(MakeCandle(idx++, o, c + 0.0002, o - 0.0003, c));
                price = c;
            }

            // Base: 2 boring candles (small body relative to range)
            for (int i = 0; i < 2; i++)
            {
                double o = price;
                double c = price + 0.0001; // tiny body
                // Boring: body = 0.0001, range = 0.0020, body*2 = 0.0002 < 0.0020
                candles.Add(MakeCandle(idx++, o, o + 0.0010, o - 0.0010, c));
                price = c;
            }

            // Leg-out: 2 exciting rally candles
            for (int i = 0; i < 2; i++)
            {
                double o = price;
                double c = price + 0.0050;
                candles.Add(MakeCandle(idx++, o, c + 0.0002, o - 0.0003, c));
                price = c;
            }

            // Gap between zones: a few boring candles to reset state machine
            for (int i = 0; i < 3; i++)
            {
                double o = price;
                double c = price + 0.0001;
                candles.Add(MakeCandle(idx++, o, o + 0.0012, o - 0.0012, c));
                price = c;
            }
        }

        // Trailing candles for trade evaluation (price continues higher)
        for (int i = 0; i < 120; i++)
        {
            double o = price;
            double c = price + 0.0003;
            candles.Add(MakeCandle(idx++, o, c + 0.0005, o - 0.0005, c));
            price = c;
        }

        return candles;
    }

    /// <summary>
    /// Build higher-timeframe trend candles showing an uptrend.
    /// Each candle represents ~4 hours, covering the same time span.
    /// </summary>
    private static List<Candlestick> BuildTrendCandles(int count = 80)
    {
        var candles = new List<Candlestick>();
        double price = 1.0900;

        for (int i = 0; i < count; i++)
        {
            double o = price;
            double c = price + 0.0020;
            candles.Add(new Candlestick
            {
                Time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddHours(i * 4)
                    .ToString("O", CultureInfo.InvariantCulture),
                Mid = new CandlestickData { O = o, H = c + 0.0005, L = o - 0.0005, C = c }
            });
            // Add some swing structure for trend detection
            if (i % 5 == 4)
                price -= 0.0030; // pullback
            else
                price = c;
        }

        return candles;
    }

    // ──────────────────────── ComputeScore unit tests ────────────────────────

    [Fact]
    public void ComputeScore_WeightedFormula_IsCorrect()
    {
        // winRate × 0.50 + zoneScore × 0.25 + rrScore × 0.25
        // zoneScore = min(traded, 100) / 100
        // rrScore   = min(avgRR, 3.0) / 3.0
        double winRate = 0.60;
        int tradedZones = 50;
        double avgRR = 1.5;
        int minZones = 10;

        double expected = 0.60 * 0.50 + (50.0 / 100.0) * 0.25 + (1.5 / 3.0) * 0.25;
        double actual = BacktestResult.ComputeScore(winRate, tradedZones, avgRR, minZones);

        Assert.Equal(expected, actual, precision: 10);
    }

    [Fact]
    public void ComputeScore_BelowMinZones_ReturnsZero()
    {
        double score = BacktestResult.ComputeScore(0.80, 5, 2.0, 10);
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ComputeScore_ZoneCountCappedAt100()
    {
        double s1 = BacktestResult.ComputeScore(0.50, 100, 1.0, 10);
        double s2 = BacktestResult.ComputeScore(0.50, 200, 1.0, 10);
        Assert.Equal(s1, s2, precision: 10);
    }

    [Fact]
    public void ComputeScore_AvgRRCappedAt3()
    {
        double s1 = BacktestResult.ComputeScore(0.50, 20, 3.0, 10);
        double s2 = BacktestResult.ComputeScore(0.50, 20, 5.0, 10);
        Assert.Equal(s1, s2, precision: 10);
    }

    [Fact]
    public void ComputeScore_PerfectScore()
    {
        // 100% win rate, 100+ zones, 3.0+ RR → max = 0.50 + 0.25 + 0.25 = 1.0
        double score = BacktestResult.ComputeScore(1.0, 100, 3.0, 10);
        Assert.Equal(1.0, score, precision: 10);
    }

    // ──────────────────────── Optimizer integration tests ────────────────────────

    [Fact]
    public void Optimize_EmptyCandles_ReturnsEmptyResults()
    {
        var optimizer = new StrategyOptimizer();
        var result = optimizer.Optimize(
            new List<Candlestick>(),
            new List<Candlestick>(),
            topN: 5);

        Assert.Empty(result.TopResults);
        Assert.Null(result.BestResult);
        Assert.Equal(0, result.ScoredCombinations);
    }

    [Fact]
    public void Optimize_ResultsSortedByScoreDescending()
    {
        var zoneCandles = BuildZoneProducingCandles(15);
        var trendCandles = BuildTrendCandles();

        var config = new BacktestConfig { MinZonesForScoring = 1, FilterByTrend = false };
        var optimizer = new StrategyOptimizer(config);

        var result = optimizer.Optimize(zoneCandles, trendCandles, topN: 20);

        if (result.TopResults.Count >= 2)
        {
            for (int i = 1; i < result.TopResults.Count; i++)
            {
                Assert.True(
                    result.TopResults[i - 1].Score >= result.TopResults[i].Score,
                    $"Result at index {i - 1} (score {result.TopResults[i - 1].Score}) " +
                    $"should be >= result at index {i} (score {result.TopResults[i].Score})");
            }
        }
    }

    [Fact]
    public void Optimize_TopN_LimitsReturnedResults()
    {
        var zoneCandles = BuildZoneProducingCandles(15);
        var trendCandles = BuildTrendCandles();

        var config = new BacktestConfig { MinZonesForScoring = 1, FilterByTrend = false };
        var optimizer = new StrategyOptimizer(config);

        var result = optimizer.Optimize(zoneCandles, trendCandles, topN: 3);

        Assert.True(result.TopResults.Count <= 3,
            $"Expected at most 3 results but got {result.TopResults.Count}");

        // With enough scored combos, we should actually get exactly 3
        if (result.ScoredCombinations >= 3)
        {
            Assert.Equal(3, result.TopResults.Count);
        }
    }

    [Fact]
    public void Optimize_BestResult_IsFirstInTopResults()
    {
        var zoneCandles = BuildZoneProducingCandles(15);
        var trendCandles = BuildTrendCandles();

        var config = new BacktestConfig { MinZonesForScoring = 1, FilterByTrend = false };
        var optimizer = new StrategyOptimizer(config);

        var result = optimizer.Optimize(zoneCandles, trendCandles, topN: 10);

        if (result.TopResults.Count > 0)
        {
            Assert.NotNull(result.BestResult);
            Assert.Equal(result.TopResults[0].Score, result.BestResult!.Score);
        }
    }

    [Fact]
    public void Optimize_InsufficientData_ReturnsFewerResults()
    {
        // Only a few candles — unlikely to produce enough zones for any config to score
        var fewCandles = Enumerable.Range(0, 10)
            .Select(i => MakeCandle(i, 1.0 + i * 0.001, 1.001 + i * 0.001, 0.999 + i * 0.001, 1.0 + i * 0.001))
            .ToList();
        var trendCandles = fewCandles;

        var config = new BacktestConfig { MinZonesForScoring = 10 };
        var optimizer = new StrategyOptimizer(config);

        var result = optimizer.Optimize(fewCandles, trendCandles, topN: 10);

        // With high MinZonesForScoring and few candles, no combos should score
        Assert.Equal(0, result.ScoredCombinations);
        Assert.Empty(result.TopResults);
    }

    [Fact]
    public void Optimize_TotalCombinations_MatchesExpected()
    {
        var optimizer = new StrategyOptimizer();
        var result = optimizer.Optimize(
            new List<Candlestick>(),
            new List<Candlestick>(),
            topN: 5);

        Assert.Equal(optimizer.TotalCombinations, result.TotalCombinations);
        // Should evaluate all combos even with empty data
        Assert.Equal(optimizer.TotalCombinations, result.EvaluatedCombinations);
    }

    [Fact]
    public void Optimize_CancellationToken_StopsExecution()
    {
        var zoneCandles = BuildZoneProducingCandles(15);
        var trendCandles = BuildTrendCandles();
        var cts = new CancellationTokenSource();

        var config = new BacktestConfig { MinZonesForScoring = 1, FilterByTrend = false };
        var optimizer = new StrategyOptimizer(config);

        // Cancel after first progress report (500 combos)
        Action<int, int> progressCallback = (evaluated, total) =>
        {
            if (evaluated >= 500)
                cts.Cancel();
        };

        Assert.Throws<OperationCanceledException>(() =>
            optimizer.Optimize(zoneCandles, trendCandles, topN: 5,
                progress: progressCallback, cancellationToken: cts.Token));
    }

    [Fact]
    public void Optimize_ProgressCallback_IsInvoked()
    {
        var zoneCandles = BuildZoneProducingCandles(15);
        var trendCandles = BuildTrendCandles();

        var config = new BacktestConfig { MinZonesForScoring = 1, FilterByTrend = false };
        var optimizer = new StrategyOptimizer(config);

        var progressCalls = new List<(int evaluated, int total)>();
        Action<int, int> progressCallback = (evaluated, total) =>
            progressCalls.Add((evaluated, total));

        optimizer.Optimize(zoneCandles, trendCandles, topN: 5, progress: progressCallback);

        // Progress should be called at least once (the final call)
        Assert.NotEmpty(progressCalls);

        // Last call should report all combos evaluated
        var last = progressCalls.Last();
        Assert.Equal(optimizer.TotalCombinations, last.evaluated);
        Assert.Equal(optimizer.TotalCombinations, last.total);
    }

    [Fact]
    public void Optimize_AllScoredResults_HavePositiveScore()
    {
        var zoneCandles = BuildZoneProducingCandles(15);
        var trendCandles = BuildTrendCandles();

        var config = new BacktestConfig { MinZonesForScoring = 1, FilterByTrend = false };
        var optimizer = new StrategyOptimizer(config);

        var result = optimizer.Optimize(zoneCandles, trendCandles, topN: 50);

        foreach (var r in result.TopResults)
        {
            Assert.True(r.Score > 0, $"Score should be positive but was {r.Score}");
        }
    }

    [Fact]
    public void Optimize_DefaultBacktestConfig_DoesNotThrow()
    {
        var zoneCandles = BuildZoneProducingCandles(5);
        var trendCandles = BuildTrendCandles(40);

        var optimizer = new StrategyOptimizer();

        var exception = Record.Exception(() =>
            optimizer.Optimize(zoneCandles, trendCandles, topN: 3));

        Assert.Null(exception);
    }
}
