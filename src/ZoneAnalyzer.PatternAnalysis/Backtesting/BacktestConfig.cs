namespace ZoneAnalyzer.PatternAnalysis.Backtesting;

/// <summary>
/// Configuration for a single backtest run.
/// </summary>
public class BacktestConfig
{
    /// <summary>Assumed spread in price units (not pips) for realistic entry simulation.</summary>
    public double SpreadAssumption { get; set; } = 0.0001; // 1 pip for most pairs

    /// <summary>Number of candles to wait before timing out a trade as neutral.</summary>
    public int TimeoutCandles { get; set; } = 100;

    /// <summary>Minimum number of zones that must match a config for it to be scored.</summary>
    public int MinZonesForScoring { get; set; } = 5;

    /// <summary>Take profit as a multiple of base width.</summary>
    public double TakeProfitMultiple { get; set; } = 2.0;

    /// <summary>Whether to filter trades by trend direction (only with-trend entries).</summary>
    public bool FilterByTrend { get; set; } = true;
}
