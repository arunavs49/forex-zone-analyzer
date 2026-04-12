using ForexZoneAnalyzer.Worker.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

public class InMemoryZoneStoreTests
{
    private readonly InMemoryZoneStore _store = new();

    [Fact]
    public async Task GetZones_ReturnsEmpty_WhenNoZonesStored()
    {
        var result = await _store.GetZonesAsync("EUR_USD", "M15", CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpsertAndGet_RoundTripsZones()
    {
        var zones = new List<Zone>
        {
            CreateZone(ZoneType.Supply, 1.1000, 1.1050, "2025-01-01T00:00:00Z"),
            CreateZone(ZoneType.Demand, 1.0900, 1.0950, "2025-01-02T00:00:00Z")
        };

        await _store.UpsertZonesAsync("EUR_USD", "M15", zones, CancellationToken.None);
        var result = await _store.GetZonesAsync("EUR_USD", "M15", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(ZoneType.Supply, result[0].Type);
        Assert.Equal(ZoneType.Demand, result[1].Type);
    }

    [Fact]
    public async Task Upsert_OverwritesPreviousZones()
    {
        var original = new List<Zone>
        {
            CreateZone(ZoneType.Supply, 1.1000, 1.1050, "2025-01-01T00:00:00Z")
        };
        await _store.UpsertZonesAsync("EUR_USD", "M15", original, CancellationToken.None);

        var updated = new List<Zone>
        {
            CreateZone(ZoneType.Demand, 1.0800, 1.0850, "2025-01-03T00:00:00Z"),
            CreateZone(ZoneType.Demand, 1.0700, 1.0750, "2025-01-04T00:00:00Z")
        };
        await _store.UpsertZonesAsync("EUR_USD", "M15", updated, CancellationToken.None);

        var result = await _store.GetZonesAsync("EUR_USD", "M15", CancellationToken.None);
        Assert.Equal(2, result.Count);
        Assert.All(result, z => Assert.Equal(ZoneType.Demand, z.Type));
    }

    [Fact]
    public async Task DifferentInstruments_AreIsolated()
    {
        var eurZones = new List<Zone> { CreateZone(ZoneType.Supply, 1.1000, 1.1050, "2025-01-01T00:00:00Z") };
        var gbpZones = new List<Zone> { CreateZone(ZoneType.Demand, 1.2500, 1.2550, "2025-01-01T00:00:00Z") };

        await _store.UpsertZonesAsync("EUR_USD", "M15", eurZones, CancellationToken.None);
        await _store.UpsertZonesAsync("GBP_USD", "M15", gbpZones, CancellationToken.None);

        var eur = await _store.GetZonesAsync("EUR_USD", "M15", CancellationToken.None);
        var gbp = await _store.GetZonesAsync("GBP_USD", "M15", CancellationToken.None);

        Assert.Single(eur);
        Assert.Equal(ZoneType.Supply, eur[0].Type);
        Assert.Single(gbp);
        Assert.Equal(ZoneType.Demand, gbp[0].Type);
    }

    [Fact]
    public async Task DifferentGranularities_AreIsolated()
    {
        var m15Zones = new List<Zone> { CreateZone(ZoneType.Supply, 1.1000, 1.1050, "2025-01-01T00:00:00Z") };
        var h1Zones = new List<Zone> { CreateZone(ZoneType.Demand, 1.0900, 1.0950, "2025-01-01T00:00:00Z") };

        await _store.UpsertZonesAsync("EUR_USD", "M15", m15Zones, CancellationToken.None);
        await _store.UpsertZonesAsync("EUR_USD", "H1", h1Zones, CancellationToken.None);

        var m15 = await _store.GetZonesAsync("EUR_USD", "M15", CancellationToken.None);
        var h1 = await _store.GetZonesAsync("EUR_USD", "H1", CancellationToken.None);

        Assert.Single(m15);
        Assert.Equal(ZoneType.Supply, m15[0].Type);
        Assert.Single(h1);
        Assert.Equal(ZoneType.Demand, h1[0].Type);
    }

    [Fact]
    public async Task GetZones_ReturnsCopy_NotReference()
    {
        var zones = new List<Zone> { CreateZone(ZoneType.Supply, 1.1000, 1.1050, "2025-01-01T00:00:00Z") };
        await _store.UpsertZonesAsync("EUR_USD", "M15", zones, CancellationToken.None);

        var result1 = await _store.GetZonesAsync("EUR_USD", "M15", CancellationToken.None);
        result1.Clear(); // Mutate the returned list

        var result2 = await _store.GetZonesAsync("EUR_USD", "M15", CancellationToken.None);
        Assert.Single(result2); // Original should be unaffected
    }

    [Fact]
    public async Task GetTrend_ReturnsNull_WhenNoTrendStored()
    {
        var result = await _store.GetTrendAsync("EUR_USD", "H1", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertAndGetTrend_RoundTrips()
    {
        await _store.UpsertTrendAsync("EUR_USD", "H1", "Up", CancellationToken.None);
        var result = await _store.GetTrendAsync("EUR_USD", "H1", CancellationToken.None);
        Assert.Equal("Up", result);
    }

    [Fact]
    public async Task UpsertTrend_OverwritesPrevious()
    {
        await _store.UpsertTrendAsync("EUR_USD", "H1", "Up", CancellationToken.None);
        await _store.UpsertTrendAsync("EUR_USD", "H1", "Down", CancellationToken.None);
        var result = await _store.GetTrendAsync("EUR_USD", "H1", CancellationToken.None);
        Assert.Equal("Down", result);
    }

    [Fact]
    public async Task Trends_DifferentGranularities_AreIsolated()
    {
        await _store.UpsertTrendAsync("EUR_USD", "H1", "Up", CancellationToken.None);
        await _store.UpsertTrendAsync("EUR_USD", "H4", "Down", CancellationToken.None);

        Assert.Equal("Up", await _store.GetTrendAsync("EUR_USD", "H1", CancellationToken.None));
        Assert.Equal("Down", await _store.GetTrendAsync("EUR_USD", "H4", CancellationToken.None));
    }

    private static Zone CreateZone(ZoneType type, double low, double high, string startTime) => new()
    {
        Type = type,
        BaseRangeLow = low,
        BaseRangeHigh = high,
        StartTime = DateTime.Parse(startTime),
        EndTime = DateTime.Parse(startTime).AddHours(2),
        BaseCandleCount = 3,
        Freshness = ZoneFreshness.Untested,
        SubZone = false
    };
}
