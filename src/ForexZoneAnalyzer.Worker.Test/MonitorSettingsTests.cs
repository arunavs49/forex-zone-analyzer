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
    public void Defaults_HaveSixTimeframePairs()
    {
        var settings = new MonitorSettings();
        Assert.Equal(6, settings.Timeframes.Length);
    }

    [Theory]
    [InlineData(0, "M5",  "M30")]
    [InlineData(1, "M15", "H1")]
    [InlineData(2, "M30", "H4")]
    [InlineData(3, "H1",  "H8")]
    [InlineData(4, "H4",  "D")]
    [InlineData(5, "D",   "W")]
    public void Defaults_TimeframePair_HasCorrectMapping(int index, string expectedZone, string expectedTrend)
    {
        var settings = new MonitorSettings();
        Assert.Equal(expectedZone, settings.Timeframes[index].ZoneGranularity);
        Assert.Equal(expectedTrend, settings.Timeframes[index].TrendGranularity);
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
