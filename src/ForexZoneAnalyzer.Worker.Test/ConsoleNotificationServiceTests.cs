using ForexZoneAnalyzer.Worker.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

public class ConsoleNotificationServiceTests
{
    [Fact]
    public void FormatAlert_IncludesInstrumentAndGranularity()
    {
        var zone = CreateZone(ZoneType.Supply, 1.0850, 1.0870);
        var result = ConsoleNotificationService.FormatAlert("EUR_USD", "M15", zone, "Up");

        Assert.Contains("EUR_USD", result);
        Assert.Contains("M15", result);
    }

    [Fact]
    public void FormatAlert_IncludesZoneType()
    {
        var zone = CreateZone(ZoneType.Supply, 1.0850, 1.0870);
        var result = ConsoleNotificationService.FormatAlert("EUR_USD", "M15", zone, "Up");

        Assert.Contains("Supply", result);
    }

    [Fact]
    public void FormatAlert_IncludesTrend()
    {
        var zone = CreateZone(ZoneType.Demand, 1.0800, 1.0820);
        var result = ConsoleNotificationService.FormatAlert("GBP_USD", "H1", zone, "Down");

        Assert.Contains("Down", result);
    }

    [Fact]
    public void FormatAlert_IncludesBaseRange()
    {
        var zone = CreateZone(ZoneType.Supply, 1.08500, 1.08700);
        var result = ConsoleNotificationService.FormatAlert("EUR_USD", "M15", zone, "Up");

        Assert.Contains("1.08500", result);
        Assert.Contains("1.08700", result);
    }

    [Fact]
    public void FormatAlert_IncludesFreshness()
    {
        var zone = CreateZone(ZoneType.Demand, 1.0800, 1.0820);
        zone.Freshness = ZoneFreshness.Tested;
        var result = ConsoleNotificationService.FormatAlert("EUR_USD", "M15", zone, "Up");

        Assert.Contains("Tested", result);
    }

    [Fact]
    public void FormatAlert_ShowsSubZoneYes_WhenTrue()
    {
        var zone = CreateZone(ZoneType.Supply, 1.0850, 1.0870);
        zone.SubZone = true;
        var result = ConsoleNotificationService.FormatAlert("EUR_USD", "M15", zone, "Up");

        Assert.Contains("Yes", result);
    }

    [Fact]
    public void FormatAlert_ShowsSubZoneNo_WhenFalse()
    {
        var zone = CreateZone(ZoneType.Supply, 1.0850, 1.0870);
        zone.SubZone = false;
        var result = ConsoleNotificationService.FormatAlert("EUR_USD", "M15", zone, "Up");

        Assert.Contains("No", result);
    }

    private static Zone CreateZone(ZoneType type, double low, double high) => new()
    {
        Type = type,
        BaseRangeLow = low,
        BaseRangeHigh = high,
        StartTime = DateTime.UtcNow.AddHours(-2),
        EndTime = DateTime.UtcNow.AddHours(-1),
        BaseCandleCount = 3,
        Freshness = ZoneFreshness.Untested,
        SubZone = false
    };
}
