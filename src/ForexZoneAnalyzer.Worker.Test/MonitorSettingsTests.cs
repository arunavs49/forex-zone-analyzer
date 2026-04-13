using ForexZoneAnalyzer.Worker.Configuration;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

public class MonitorSettingsTests
{
    [Fact]
    public void Defaults_HaveEmptyInstruments()
    {
        var settings = new MonitorSettings();
        Assert.Empty(settings.Instruments);
    }

    [Fact]
    public void Defaults_HaveEmptyTimeframes()
    {
        var settings = new MonitorSettings();
        Assert.Empty(settings.Timeframes);
    }

    [Fact]
    public void Defaults_CandleCacheSize_Is2000()
    {
        var settings = new MonitorSettings();
        Assert.Equal(2000, settings.CandleCacheSize);
    }

    [Fact]
    public void Defaults_CandleOverlapCount_Is5()
    {
        var settings = new MonitorSettings();
        Assert.Equal(5, settings.CandleOverlapCount);
    }

    [Fact]
    public void CanSetInstrumentsAndTimeframes()
    {
        var settings = new MonitorSettings
        {
            Instruments = ["EUR_USD", "GBP_USD"],
            Timeframes =
            [
                new() { ZoneGranularity = "M5", TrendGranularity = "M30" },
                new() { ZoneGranularity = "H1", TrendGranularity = "H8" },
            ]
        };
        Assert.Equal(2, settings.Instruments.Length);
        Assert.Equal(2, settings.Timeframes.Length);
        Assert.Equal("M5", settings.Timeframes[0].ZoneGranularity);
        Assert.Equal("H8", settings.Timeframes[1].TrendGranularity);
    }
}
