namespace ZoneAnalyzer.PatternAnalysis
{
    /// <summary>
    /// Configuration for trend detection via swing point analysis.
    /// </summary>
    public class TrendConfiguration
    {
        /// <summary>
        /// Number of candles on each side of a pivot to confirm a swing point.
        /// Higher values produce fewer, more significant swing points.
        /// Recommended: 2 for scalping, 3 for M15/H1, 5 for H4/Daily.
        /// </summary>
        public int SwingLookback { get; set; } = 3;

        /// <summary>
        /// Number of candles to consider for trend analysis (from most recent).
        /// </summary>
        public int TrendCandleCount { get; set; } = 60;

        /// <summary>
        /// Minimum number of swing highs and swing lows required to determine trend.
        /// If fewer swing points are found, returns Sideways.
        /// </summary>
        public int MinSwingPoints { get; set; } = 2;
    }
}
