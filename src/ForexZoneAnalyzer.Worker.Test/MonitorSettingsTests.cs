using ForexZoneAnalyzer.Worker.Configuration;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

public class MonitorSettingsTests
{
    [Fact]
    public void Defaults_HaveSevenInstruments()
    {
        var settings = new MonitorSettings();
        Assert.Equal(7, settings.Instruments.Length);
        Assert.Contains("EUR_USD", settings.Instruments);
        Assert.Contains("GBP_USD", settings.Instruments);
        Assert.Contains("USD_JPY", settings.Instruments);
        Assert.Contains("AUD_USD", settings.Instruments);
        Assert.Contains("NZD_USD", settings.Instruments);
        Assert.Contains("USD_CAD", settings.Instruments);
        Assert.Contains("USD_CHF", settings.Instruments);
    }

    [Fact]
    public void Defaults_ZoneGranularity_IsH1()
    {
        var settings = new MonitorSettings();
        Assert.Equal("H1", settings.ZoneGranularity);
    }

    [Fact]
    public void Defaults_TrendGranularity_IsH8()
    {
        var settings = new MonitorSettings();
        Assert.Equal("H8", settings.TrendGranularity);
    }

    [Fact]
    public void Defaults_PollInterval_Is60Minutes()
    {
        var settings = new MonitorSettings();
        Assert.Equal(60, settings.PollIntervalMinutes);
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
