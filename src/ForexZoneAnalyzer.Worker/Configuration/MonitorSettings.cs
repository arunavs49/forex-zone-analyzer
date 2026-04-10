namespace ForexZoneAnalyzer.Worker.Configuration;

public class MonitorSettings
{
    public string[] Instruments { get; set; } = ["EUR_USD", "GBP_USD", "USD_JPY", "AUD_USD"];
    public string ZoneGranularity { get; set; } = "M15";
    public string TrendGranularity { get; set; } = "H1";
    public int PollIntervalMinutes { get; set; } = 15;
    public int CandleCacheSize { get; set; } = 2000;
    public int CandleOverlapCount { get; set; } = 5;
}
