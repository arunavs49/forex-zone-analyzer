using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis.Backtesting;

/// <summary>
/// Grid search optimizer: precomputes all candidate zones once, then evaluates
/// each ZoneConfig + TrendConfig combination by filtering the precomputed set.
/// </summary>
public class StrategyOptimizer
{
    private readonly BacktestConfig _backtestConfig;

    // Zone detection parameter grid
    private static readonly int[] MinBaseLengths = { 1, 2, 3 };
    private static readonly int[] MaxBaseLengths = { 3, 4, 5, 6, 7, 8 };
    private static readonly double[] LegInRatios = { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };
    private static readonly double[] LegOutRatios = { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };

    // Trend detection parameter grid
    private static readonly int[] SwingLookbacks = { 2, 3, 4, 5 };
    private static readonly int[] TrendCandleCounts = { 30, 45, 60, 80, 100 };
    private static readonly int[] MinSwingPointsList = { 2, 3 };

    public StrategyOptimizer(BacktestConfig? backtestConfig = null)
    {
        _backtestConfig = backtestConfig ?? new BacktestConfig();
    }

    public int TotalCombinations => MinBaseLengths.Length * MaxBaseLengths.Length
        * LegInRatios.Length * LegOutRatios.Length
        * SwingLookbacks.Length * TrendCandleCounts.Length * MinSwingPointsList.Length;

    /// <summary>
    /// Run grid search optimization over all parameter combinations.
    /// </summary>
    /// <param name="zoneCandles">Zone timeframe candles (sorted ascending)</param>
    /// <param name="trendCandles">Higher TF candles (sorted ascending)</param>
    /// <param name="topN">Number of top results to keep</param>
    /// <param name="progress">Optional callback for progress reporting (combos evaluated so far)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Top N results sorted by score descending</returns>
    public OptimizationResult Optimize(
        List<Candlestick> zoneCandles,
        List<Candlestick> trendCandles,
        int topN = 10,
        Action<int, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Precompute ALL candidate zones once (no config filtering)
        var sorted = zoneCandles.OrderBy(c => c.Time);
        var finder = ZoneFinder.Create(sorted);
        var allZones = finder.GetAllZones();

        var engine = new BacktestEngine(_backtestConfig);
        var results = new List<BacktestResult>();
        var evaluated = 0;
        var total = TotalCombinations;

        // Step 2: Grid search — filter zones per config combo
        foreach (var minBase in MinBaseLengths)
        foreach (var maxBase in MaxBaseLengths)
        {
            if (minBase > maxBase) continue; // Skip invalid combos

            foreach (var legIn in LegInRatios)
            foreach (var legOut in LegOutRatios)
            foreach (var swingLookback in SwingLookbacks)
            foreach (var trendCount in TrendCandleCounts)
            foreach (var minSwing in MinSwingPointsList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var zoneConfig = new ZoneConfiguration
                {
                    MinBaseLength = minBase,
                    MaxBaseLength = maxBase,
                    MinLegInToBaseRangeRatio = legIn,
                    MinLegOutToBaseRangeRatio = legOut
                };

                var trendConfig = new TrendConfiguration
                {
                    SwingLookback = swingLookback,
                    TrendCandleCount = trendCount,
                    MinSwingPoints = minSwing
                };

                var result = engine.Evaluate(allZones, zoneCandles, trendCandles, zoneConfig, trendConfig);
                if (result.Score > 0)
                    results.Add(result);

                evaluated++;
                if (evaluated % 500 == 0)
                    progress?.Invoke(evaluated, total);
            }
        }

        progress?.Invoke(evaluated, total);

        var topResults = results
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();

        return new OptimizationResult
        {
            TotalCombinations = total,
            EvaluatedCombinations = evaluated,
            ScoredCombinations = results.Count,
            TopResults = topResults,
            BestResult = topResults.FirstOrDefault()
        };
    }
}

public class OptimizationResult
{
    public int TotalCombinations { get; set; }
    public int EvaluatedCombinations { get; set; }
    public int ScoredCombinations { get; set; }
    public List<BacktestResult> TopResults { get; set; } = new();
    public BacktestResult? BestResult { get; set; }
}
