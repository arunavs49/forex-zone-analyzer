namespace ForexZoneAnalyzer.Worker.Configuration;

public class MonitorSettings
{
    public string[] Instruments { get; set; } = [
        "EUR_USD", "GBP_USD", "USD_JPY", "AUD_USD",
        "NZD_USD", "USD_CAD", "USD_CHF"
    ];
    public string ZoneGranularity { get; set; } = "H1";
    public string TrendGranularity { get; set; } = "H8";
    public int PollIntervalMinutes { get; set; } = 60;
    public int CandleCacheSize { get; set; } = 2000;
    public int CandleOverlapCount { get; set; } = 5;
}
