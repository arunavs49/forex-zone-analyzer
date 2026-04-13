namespace ForexZoneAnalyzer.Worker.Configuration;

public class MonitorSettings
{
    public string[] Instruments { get; set; } = [];

    /// <summary>
    /// Zone/trend granularity pairs processed each cycle.
    /// Each entry maps a zone timeframe to its higher-timeframe trend.
    /// Configured via appsettings.json — no code defaults to avoid merge issues.
    /// </summary>
    public TimeframePair[] Timeframes { get; set; } = [];

    public int CandleCacheSize { get; set; } = 2000;
    public int CandleOverlapCount { get; set; } = 5;
}

public class TimeframePair
{
    public string ZoneGranularity { get; set; } = "";
    public string TrendGranularity { get; set; } = "";
}
