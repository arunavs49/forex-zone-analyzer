namespace ForexZoneAnalyzer.Worker.Configuration;

public class MonitorSettings
{
    public string[] Instruments { get; set; } = [
        "EUR_USD", "GBP_USD", "USD_JPY", "AUD_USD",
        "NZD_USD", "USD_CAD", "USD_CHF"
    ];

    /// <summary>
    /// Zone/trend granularity pairs processed each cycle.
    /// Each entry maps a zone timeframe to its higher-timeframe trend.
    /// </summary>
    public TimeframePair[] Timeframes { get; set; } = [
        new() { ZoneGranularity = "M5",  TrendGranularity = "M30" },
        new() { ZoneGranularity = "M15", TrendGranularity = "H1"  },
        new() { ZoneGranularity = "M30", TrendGranularity = "H4"  },
        new() { ZoneGranularity = "H1",  TrendGranularity = "H8"  },
        new() { ZoneGranularity = "H4",  TrendGranularity = "D"   },
        new() { ZoneGranularity = "D",   TrendGranularity = "W"   },
    ];

    public int PollIntervalMinutes { get; set; } = 5;
    public int CandleCacheSize { get; set; } = 2000;
    public int CandleOverlapCount { get; set; } = 5;
}

public class TimeframePair
{
    public string ZoneGranularity { get; set; } = "";
    public string TrendGranularity { get; set; } = "";
}
