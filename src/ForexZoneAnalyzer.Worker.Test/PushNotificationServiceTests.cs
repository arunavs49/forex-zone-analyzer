using System.Text.Json;
using ForexZoneAnalyzer.Worker.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

public class PushNotificationServiceTests
{
    [Fact]
    public void BuildApnsPayload_ContainsExpectedFields()
    {
        var zone = new Zone
        {
            Type = ZoneType.Demand,
            Freshness = ZoneFreshness.Untested,
            SubZone = false,
            BaseRangeLow = 1.08500,
            BaseRangeHigh = 1.08700,
            BaseCandleCount = 3,
            StartTime = DateTime.Parse("2026-04-10T12:00:00Z").ToUniversalTime(),
            EndTime = DateTime.Parse("2026-04-10T13:00:00Z").ToUniversalTime()
        };

        var payload = PushNotificationService.BuildApnsPayload("EUR_USD", "M15", zone, "Bullish");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        // aps.alert
        var aps = root.GetProperty("aps");
        var alert = aps.GetProperty("alert");
        Assert.Contains("Demand", alert.GetProperty("title").GetString());
        Assert.Contains("EUR_USD", alert.GetProperty("subtitle").GetString());
        Assert.Contains("1.08500", alert.GetProperty("body").GetString());
        Assert.Equal("default", aps.GetProperty("sound").GetString());
        Assert.Equal("ZONE_ALERT", aps.GetProperty("category").GetString());

        // zone metadata
        var zoneData = root.GetProperty("zone");
        Assert.Equal("EUR_USD", zoneData.GetProperty("instrument").GetString());
        Assert.Equal("M15", zoneData.GetProperty("granularity").GetString());
        Assert.Equal("Demand", zoneData.GetProperty("type").GetString());
        Assert.Equal("Untested", zoneData.GetProperty("freshness").GetString());
        Assert.False(zoneData.GetProperty("subZone").GetBoolean());
        Assert.Equal("Bullish", zoneData.GetProperty("trend").GetString());
    }

    [Fact]
    public void BuildApnsPayload_SupplyZone_HasCorrectTitle()
    {
        var zone = new Zone
        {
            Type = ZoneType.Supply,
            Freshness = ZoneFreshness.Tested,
            SubZone = true,
            BaseRangeLow = 150.500,
            BaseRangeHigh = 150.800,
            BaseCandleCount = 2,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow
        };

        var payload = PushNotificationService.BuildApnsPayload("USD_JPY", "H1", zone, "Bearish");

        using var doc = JsonDocument.Parse(payload);
        var alert = doc.RootElement.GetProperty("aps").GetProperty("alert");
        Assert.Contains("Supply", alert.GetProperty("title").GetString());
        Assert.Contains("USD_JPY", alert.GetProperty("subtitle").GetString());
    }

    [Fact]
    public void BuildApnsPayload_IsValidJson()
    {
        var zone = new Zone
        {
            Type = ZoneType.Demand,
            Freshness = ZoneFreshness.Untested,
            BaseRangeLow = 1.0,
            BaseRangeHigh = 2.0,
            BaseCandleCount = 1,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow
        };

        var payload = PushNotificationService.BuildApnsPayload("GBP_USD", "M15", zone, "Unknown");

        // Should not throw
        var parsed = JsonDocument.Parse(payload);
        Assert.NotNull(parsed);
    }
}
