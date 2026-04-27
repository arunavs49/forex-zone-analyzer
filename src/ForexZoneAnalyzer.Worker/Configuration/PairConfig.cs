namespace ForexZoneAnalyzer.Worker.Configuration;

/// <summary>
/// Per-instrument-per-timeframe configuration for zone and trend detection.
/// Stored in Azure Table Storage (pairconfigs table).
/// PartitionKey = instrument, RowKey = zone granularity.
/// </summary>
public class PairConfig
{
    public string Instrument { get; set; } = "";
    public string ZoneGranularity { get; set; } = "";
    public string TrendGranularity { get; set; } = "";

    // Processing control
    public bool Enabled { get; set; }
    public bool EmailEnabled { get; set; }

    // Zone detection parameters
    public int MinBaseLength { get; set; } = 1;
    public int MaxBaseLength { get; set; } = 6;
    public double MinLegInToBaseRangeRatio { get; set; } = 1.0;
    public double MinLegOutToBaseRangeRatio { get; set; } = 1.0;

    // Trend detection parameters
    public int SwingLookback { get; set; } = 3;
    public int TrendCandleCount { get; set; } = 60;
    public int MinSwingPoints { get; set; } = 2;

    // Versioning (incremented on config change; triggers zone refresh)
    public int ConfigVersion { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ZoneAnalyzer.PatternAnalysis.ZoneConfiguration ToZoneConfiguration() => new()
    {
        MinBaseLength = MinBaseLength,
        MaxBaseLength = MaxBaseLength,
        MinLegInToBaseRangeRatio = MinLegInToBaseRangeRatio,
        MinLegOutToBaseRangeRatio = MinLegOutToBaseRangeRatio
    };

    public ZoneAnalyzer.PatternAnalysis.TrendConfiguration ToTrendConfiguration() => new()
    {
        SwingLookback = SwingLookback,
        TrendCandleCount = TrendCandleCount,
        MinSwingPoints = MinSwingPoints
    };
}
