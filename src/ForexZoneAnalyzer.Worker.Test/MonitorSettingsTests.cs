using ForexZoneAnalyzer.Worker.Configuration;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

public class MonitorSettingsTests
{
    [Fact]
    public void Defaults_HaveFourInstruments()
    {
        var settings = new MonitorSettings();
        Assert.Equal(4, settings.Instruments.Length);
        Assert.Contains("EUR_USD", settings.Instruments);
        Assert.Contains("GBP_USD", settings.Instruments);
        Assert.Contains("USD_JPY", settings.Instruments);
        Assert.Contains("AUD_USD", settings.Instruments);
    }

    [Fact]
    public void Defaults_ZoneGranularity_IsM15()
    {
        var settings = new MonitorSettings();
        Assert.Equal("M15", settings.ZoneGranularity);
    }

    [Fact]
    public void Defaults_TrendGranularity_IsH1()
    {
        var settings = new MonitorSettings();
        Assert.Equal("H1", settings.TrendGranularity);
    }

    [Fact]
    public void Defaults_PollInterval_Is15Minutes()
    {
        var settings = new MonitorSettings();
        Assert.Equal(15, settings.PollIntervalMinutes);
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
}
