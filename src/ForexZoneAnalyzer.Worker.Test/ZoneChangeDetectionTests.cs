using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

/// <summary>
/// Tests the zone key generation and change detection logic used by ZoneMonitorService.
/// The GetZoneKey pattern is: "{Type}_{StartTime:O}_{BaseRangeHigh}_{BaseRangeLow}"
/// </summary>
public class ZoneChangeDetectionTests
{
    [Fact]
    public void SameZone_ProducesSameKey()
    {
        var zone1 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        var zone2 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");

        Assert.Equal(GetZoneKey(zone1), GetZoneKey(zone2));
    }

    [Fact]
    public void DifferentType_ProducesDifferentKey()
    {
        var supply = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        var demand = CreateZone(ZoneType.Demand, 1.0850, 1.0870, "2025-01-15T10:00:00Z");

        Assert.NotEqual(GetZoneKey(supply), GetZoneKey(demand));
    }

    [Fact]
    public void DifferentStartTime_ProducesDifferentKey()
    {
        var zone1 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        var zone2 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:15:00Z");

        Assert.NotEqual(GetZoneKey(zone1), GetZoneKey(zone2));
    }

    [Fact]
    public void DifferentBaseRange_ProducesDifferentKey()
    {
        var zone1 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        var zone2 = CreateZone(ZoneType.Supply, 1.0860, 1.0880, "2025-01-15T10:00:00Z");

        Assert.NotEqual(GetZoneKey(zone1), GetZoneKey(zone2));
    }

    [Fact]
    public void DifferentFreshness_DoesNotAffectKey()
    {
        var zone1 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        zone1.Freshness = ZoneFreshness.Untested;

        var zone2 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        zone2.Freshness = ZoneFreshness.Tested;

        Assert.Equal(GetZoneKey(zone1), GetZoneKey(zone2));
    }

    [Fact]
    public void DifferentSubZone_DoesNotAffectKey()
    {
        var zone1 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        zone1.SubZone = false;

        var zone2 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        zone2.SubZone = true;

        Assert.Equal(GetZoneKey(zone1), GetZoneKey(zone2));
    }

    [Fact]
    public void NewZoneDetection_FindsZonesNotInPersistedSet()
    {
        var persisted = new List<Zone>
        {
            CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z"),
            CreateZone(ZoneType.Demand, 1.0700, 1.0720, "2025-01-14T08:00:00Z")
        };

        var fresh = new List<Zone>
        {
            CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z"), // existing
            CreateZone(ZoneType.Demand, 1.0700, 1.0720, "2025-01-14T08:00:00Z"), // existing
            CreateZone(ZoneType.Supply, 1.0900, 1.0920, "2025-01-16T12:00:00Z")  // NEW
        };

        var persistedKeys = new HashSet<string>(persisted.Select(GetZoneKey));
        var newZones = fresh.Where(z => !persistedKeys.Contains(GetZoneKey(z))).ToList();

        Assert.Single(newZones);
        Assert.Equal(ZoneType.Supply, newZones[0].Type);
        Assert.Equal(1.0900, newZones[0].BaseRangeLow);
    }

    [Fact]
    public void NewZoneDetection_AllNew_WhenPersistedEmpty()
    {
        var persisted = new List<Zone>();
        var fresh = new List<Zone>
        {
            CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z"),
            CreateZone(ZoneType.Demand, 1.0700, 1.0720, "2025-01-14T08:00:00Z")
        };

        var persistedKeys = new HashSet<string>(persisted.Select(GetZoneKey));
        var newZones = fresh.Where(z => !persistedKeys.Contains(GetZoneKey(z))).ToList();

        Assert.Equal(2, newZones.Count);
    }

    [Fact]
    public void NewZoneDetection_NoneNew_WhenAllExist()
    {
        var zone1 = CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z");
        var zone2 = CreateZone(ZoneType.Demand, 1.0700, 1.0720, "2025-01-14T08:00:00Z");

        var persisted = new List<Zone> { zone1, zone2 };
        // Fresh set has same zones (possibly with updated properties)
        var fresh = new List<Zone>
        {
            CreateZone(ZoneType.Supply, 1.0850, 1.0870, "2025-01-15T10:00:00Z"),
            CreateZone(ZoneType.Demand, 1.0700, 1.0720, "2025-01-14T08:00:00Z")
        };

        var persistedKeys = new HashSet<string>(persisted.Select(GetZoneKey));
        var newZones = fresh.Where(z => !persistedKeys.Contains(GetZoneKey(z))).ToList();

        Assert.Empty(newZones);
    }

    /// <summary>
    /// Mirrors ZoneMonitorService.GetZoneKey exactly
    /// </summary>
    private static string GetZoneKey(Zone zone) =>
        $"{zone.Type}_{zone.StartTime:O}_{zone.BaseRangeHigh}_{zone.BaseRangeLow}";

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
