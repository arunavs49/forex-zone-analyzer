using System.Collections.Generic;
using System.Linq;
using Xunit;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis.Test
{
    public class ZoneFinderSubZoneTests
    {
        private static Candlestick C(string time, double o, double h, double l, double c)
        {
            return new Candlestick(time, mid: new CandlestickData(o, h, l, c));
        }

        private static List<Zone> FindZones(List<Candlestick> candles)
        {
            var finder = ZoneFinder.Create(candles.OrderBy(c => c.Time));
            return finder.GetAllZones();
        }

        [Fact]
        public void NewZone_OverlappingUntestedPriorZone_IsSubZone()
        {
            // Zone 1: Demand (Drop→Base→Rally), base 1.0100–1.0200
            // Zone 2: Demand, base also 1.0100–1.0200 (100% overlap)
            // Prior zone untested between them → SubZone = true
            var candles = new List<Candlestick>
            {
                // Zone 1
                C("2025-01-01T01:00:00Z", 1.0400, 1.0400, 1.0200, 1.0200), // ExcitingDrop leg-in
                C("2025-01-01T02:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T03:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                // Zone 2 (ExcitingDrop emits Z1 and starts Z2 leg-in)
                C("2025-01-01T04:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop emits Z1
                C("2025-01-01T05:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base (overlaps Z1)
                C("2025-01-01T06:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                C("2025-01-01T07:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop emits Z2
            };

            var zones = FindZones(candles);
            Assert.True(zones.Count >= 2, $"Expected at least 2 zones, got {zones.Count}");

            var firstZone = zones[0];
            var secondZone = zones[1];

            Assert.False(firstZone.SubZone, "First zone should not be a sub-zone");
            Assert.True(secondZone.SubZone, "Second zone should be a sub-zone of the first");
        }

        [Fact]
        public void NewZone_NoOverlapWithPriorZone_NotSubZone()
        {
            // Zone 1: Demand base at 1.0100–1.0200
            // Zone 2: Demand base at 1.0500–1.0600 (no overlap)
            var candles = new List<Candlestick>
            {
                // Zone 1
                C("2025-01-01T01:00:00Z", 1.0400, 1.0400, 1.0200, 1.0200), // ExcitingDrop leg-in
                C("2025-01-01T02:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T03:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                // Zone 2 at higher price level (no overlap with zone 1)
                C("2025-01-01T04:00:00Z", 1.0800, 1.0800, 1.0600, 1.0600), // ExcitingDrop emits Z1
                C("2025-01-01T05:00:00Z", 1.0550, 1.0600, 1.0500, 1.0555), // Boring base
                C("2025-01-01T06:00:00Z", 1.0560, 1.0800, 1.0560, 1.0800), // ExcitingRally leg-out
                C("2025-01-01T07:00:00Z", 1.0750, 1.0750, 1.0550, 1.0550), // ExcitingDrop emits Z2
            };

            var zones = FindZones(candles);
            Assert.True(zones.Count >= 2, $"Expected at least 2 zones, got {zones.Count}");

            Assert.False(zones[0].SubZone, "First zone should not be a sub-zone");
            Assert.False(zones[1].SubZone, "Second zone should not be a sub-zone (no overlap)");
        }

        [Fact]
        public void NewZone_PriorZoneBrokenBeforeFormation_NotSubZone()
        {
            // Zone 1: Demand base at 1.0100–1.0200
            // C4 breaks zone 1 (L=1.0050 < 1.0100), then zone 2 overlaps
            // Since prior zone was broken before zone 2 formed → not a sub-zone
            var candles = new List<Candlestick>
            {
                // Zone 1
                C("2025-01-01T01:00:00Z", 1.0400, 1.0400, 1.0200, 1.0200), // ExcitingDrop leg-in
                C("2025-01-01T02:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T03:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                // Breaks zone 1 (L < 1.0100)
                C("2025-01-01T04:00:00Z", 1.0400, 1.0400, 1.0050, 1.0050), // ExcitingDrop emits Z1, breaks Z1
                // Reset state machine toward zone 2
                C("2025-01-01T05:00:00Z", 1.0050, 1.0400, 1.0050, 1.0400), // ExcitingRally restart
                C("2025-01-01T06:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop restart
                // Zone 2 overlapping zone 1's base
                C("2025-01-01T07:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T08:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                C("2025-01-01T09:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop emits Z2
            };

            var zones = FindZones(candles);
            Assert.True(zones.Count >= 2, $"Expected at least 2 zones, got {zones.Count}");

            var secondZone = zones.Last();
            Assert.False(secondZone.SubZone, "Zone overlapping a broken zone should not be a sub-zone");
        }

        [Fact]
        public void NewZone_LessThan10PercentOverlap_NotSubZone()
        {
            // Zone 1 base: 1.0100–1.0200 (range = 0.0100)
            // Zone 2 base: 1.0195–1.0300 (overlap = 0.0005 = 5% of zone 1's range → < 10%)
            var candles = new List<Candlestick>
            {
                // Zone 1
                C("2025-01-01T01:00:00Z", 1.0400, 1.0400, 1.0200, 1.0200), // ExcitingDrop leg-in
                C("2025-01-01T02:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T03:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                // Zone 2 with barely-touching base
                C("2025-01-01T04:00:00Z", 1.0500, 1.0500, 1.0300, 1.0300), // ExcitingDrop emits Z1
                C("2025-01-01T05:00:00Z", 1.0250, 1.0300, 1.0195, 1.0255), // Boring base
                C("2025-01-01T06:00:00Z", 1.0260, 1.0500, 1.0260, 1.0500), // ExcitingRally leg-out
                C("2025-01-01T07:00:00Z", 1.0450, 1.0450, 1.0250, 1.0250), // ExcitingDrop emits Z2
            };

            var zones = FindZones(candles);
            Assert.True(zones.Count >= 2, $"Expected at least 2 zones, got {zones.Count}");

            Assert.False(zones[0].SubZone, "First zone should not be a sub-zone");
            Assert.False(zones[1].SubZone, "Second zone with <10% overlap should not be a sub-zone");
        }

        [Fact]
        public void NewZone_OverlappingTestedPriorZone_IsSubZone()
        {
            // Zone 1: Demand base 1.0100–1.0200
            // C4 tests zone 1 (L=1.0150 enters base but ≥ 1.0100) — not broken
            // Zone 2 overlaps → SubZone = true
            var candles = new List<Candlestick>
            {
                // Zone 1
                C("2025-01-01T01:00:00Z", 1.0400, 1.0400, 1.0200, 1.0200), // ExcitingDrop leg-in
                C("2025-01-01T02:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T03:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                // Tests zone 1 (dips into base, L > baseRangeLow)
                C("2025-01-01T04:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop emits Z1
                C("2025-01-01T05:00:00Z", 1.0150, 1.0400, 1.0150, 1.0400), // ExcitingRally restart
                C("2025-01-01T06:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop restart
                // Zone 2 overlapping zone 1
                C("2025-01-01T07:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T08:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                C("2025-01-01T09:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop emits Z2
            };

            var zones = FindZones(candles);
            Assert.True(zones.Count >= 2, $"Expected at least 2 zones, got {zones.Count}");

            Assert.False(zones[0].SubZone, "First zone should not be a sub-zone");
            Assert.True(zones[1].SubZone, "Zone overlapping a tested (not broken) zone should be a sub-zone");
        }

        [Fact]
        public void FirstZone_AlwaysNotSubZone()
        {
            // A single zone should never be a sub-zone
            var candles = new List<Candlestick>
            {
                C("2025-01-01T01:00:00Z", 1.0400, 1.0400, 1.0200, 1.0200), // ExcitingDrop leg-in
                C("2025-01-01T02:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T03:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                C("2025-01-01T04:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop emits zone
            };

            var zones = FindZones(candles);
            Assert.True(zones.Count >= 1, $"Expected at least 1 zone, got {zones.Count}");
            Assert.False(zones[0].SubZone, "First/only zone should not be a sub-zone");
        }

        [Fact]
        public void DemandZone_OverlappingPriorSupplyZone_NotSubZone()
        {
            // Supply zone base at 1.0100–1.0200, then demand zone at same level
            // Different types → NOT a sub-zone
            var candles = new List<Candlestick>
            {
                // Zone 1: Supply (Rally→Base→Drop)
                C("2025-01-01T01:00:00Z", 0.9900, 1.0100, 0.9900, 1.0100), // ExcitingRally leg-in
                C("2025-01-01T02:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base
                C("2025-01-01T03:00:00Z", 1.0140, 1.0140, 0.9900, 0.9900), // ExcitingDrop leg-out
                // Zone 2: Demand (Drop→Base→Rally) overlapping supply zone's base
                C("2025-01-01T04:00:00Z", 1.0400, 1.0400, 1.0200, 1.0200), // ExcitingDrop emits Z1, starts Z2 leg-in
                C("2025-01-01T05:00:00Z", 1.0150, 1.0200, 1.0100, 1.0155), // Boring base (overlaps Z1)
                C("2025-01-01T06:00:00Z", 1.0160, 1.0400, 1.0160, 1.0400), // ExcitingRally leg-out
                C("2025-01-01T07:00:00Z", 1.0350, 1.0350, 1.0150, 1.0150), // ExcitingDrop emits Z2
            };

            var zones = FindZones(candles);
            Assert.True(zones.Count >= 2, $"Expected at least 2 zones, got {zones.Count}");

            var supplyZone = zones.FirstOrDefault(z => z.Type == ZoneType.Supply);
            var demandZone = zones.FirstOrDefault(z => z.Type == ZoneType.Demand);
            Assert.NotNull(supplyZone);
            Assert.NotNull(demandZone);

            Assert.False(supplyZone.SubZone, "Supply zone should not be a sub-zone");
            Assert.False(demandZone.SubZone, "Demand zone overlapping supply zone should not be a sub-zone (different types)");
        }

        [Fact]
        public void SupplyZone_OverlappingPriorSupplyZone_IsSubZone()
        {
            // Zone 1: Supply (Rally→Base→Drop), base 1.0300–1.0400
            // Zone 2: Supply, same base → SubZone = true
            var candles = new List<Candlestick>
            {
                // Zone 1
                C("2025-01-01T01:00:00Z", 1.0100, 1.0300, 1.0100, 1.0300), // ExcitingRally leg-in
                C("2025-01-01T02:00:00Z", 1.0350, 1.0400, 1.0300, 1.0355), // Boring base
                C("2025-01-01T03:00:00Z", 1.0340, 1.0340, 1.0100, 1.0100), // ExcitingDrop leg-out
                // Zone 2 (ExcitingRally emits Z1, starts Z2 leg-in)
                C("2025-01-01T04:00:00Z", 1.0100, 1.0300, 1.0100, 1.0300), // ExcitingRally emits Z1
                C("2025-01-01T05:00:00Z", 1.0350, 1.0400, 1.0300, 1.0355), // Boring base (overlaps Z1)
                C("2025-01-01T06:00:00Z", 1.0340, 1.0340, 1.0100, 1.0100), // ExcitingDrop leg-out
                C("2025-01-01T07:00:00Z", 1.0100, 1.0300, 1.0100, 1.0300), // ExcitingRally emits Z2
            };

            var zones = FindZones(candles);
            var supplyZones = zones.Where(z => z.Type == ZoneType.Supply).ToList();
            Assert.True(supplyZones.Count >= 2, $"Expected at least 2 supply zones, got {supplyZones.Count}");

            Assert.False(supplyZones[0].SubZone, "First supply zone should not be a sub-zone");
            Assert.True(supplyZones[1].SubZone, "Second supply zone overlapping first should be a sub-zone");
        }
    }
}
