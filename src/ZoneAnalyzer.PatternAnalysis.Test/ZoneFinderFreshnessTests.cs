using System.Collections.Generic;
using System.Linq;
using Xunit;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis.Test
{
    public class ZoneFinderFreshnessTests
    {
        // Helper: creates a mid-only candlestick
        private static Candlestick C(string time, double o, double h, double l, double c)
        {
            return new Candlestick(time, mid: new CandlestickData(o, h, l, c));
        }

        // Builds a supply zone (Rally-Base-Drop) with base ~1.0150–1.0250 (width 0.0100)
        // then appends the provided aftermath candles and returns ZoneFinder results.
        private static List<Zone> BuildSupplyZone(params Candlestick[] aftermath)
        {
            var candles = new List<Candlestick>
            {
                // Leg in: exciting rally (body/range = 100%)
                C("2025-01-01T01:00:00Z", 1.0000, 1.0150, 1.0000, 1.0150),
                // Base: 3 boring candles (body/range < 50%)
                C("2025-01-01T02:00:00Z", 1.0160, 1.0250, 1.0150, 1.0170),
                C("2025-01-01T03:00:00Z", 1.0170, 1.0240, 1.0160, 1.0180),
                C("2025-01-01T04:00:00Z", 1.0180, 1.0230, 1.0170, 1.0190),
                // Leg out: exciting drop (body/range = 100%)
                C("2025-01-01T05:00:00Z", 1.0180, 1.0180, 1.0030, 1.0030),
                // Boring candle to trigger zone emission
                C("2025-01-01T06:00:00Z", 1.0030, 1.0040, 1.0020, 1.0035),
            };
            candles.AddRange(aftermath);

            var finder = ZoneFinder.Create(candles.OrderBy(c => c.Time));
            return finder.GetAllZones();
        }

        // Builds a demand zone (Drop-Base-Rally) with base ~1.0100–1.0200 (width 0.0100)
        private static List<Zone> BuildDemandZone(params Candlestick[] aftermath)
        {
            var candles = new List<Candlestick>
            {
                // Leg in: exciting drop (body/range = 100%)
                C("2025-01-01T01:00:00Z", 1.0300, 1.0300, 1.0150, 1.0150),
                // Base: 3 boring candles (body/range < 50%)
                C("2025-01-01T02:00:00Z", 1.0160, 1.0200, 1.0100, 1.0170),
                C("2025-01-01T03:00:00Z", 1.0170, 1.0190, 1.0110, 1.0180),
                C("2025-01-01T04:00:00Z", 1.0180, 1.0185, 1.0120, 1.0175),
                // Leg out: exciting rally (body/range = 100%)
                C("2025-01-01T05:00:00Z", 1.0180, 1.0330, 1.0180, 1.0330),
                // Boring candle to trigger zone emission
                C("2025-01-01T06:00:00Z", 1.0330, 1.0340, 1.0320, 1.0335),
            };
            candles.AddRange(aftermath);

            var finder = ZoneFinder.Create(candles.OrderBy(c => c.Time));
            return finder.GetAllZones();
        }

        // ===== SUPPLY ZONE TESTS =====

        [Fact]
        public void SupplyZone_Untested_FreshnessUntestedWorkedNull()
        {
            // Price stays far below zone after formation
            var zones = BuildSupplyZone(
                C("2025-01-01T07:00:00Z", 1.0035, 1.0045, 1.0025, 1.0040),
                C("2025-01-01T08:00:00Z", 1.0040, 1.0050, 1.0030, 1.0045)
            );

            var supply = zones.Where(z => z.Type == ZoneType.Supply).ToList();
            Assert.Single(supply);
            Assert.Equal(ZoneFreshness.Untested, supply[0].Freshness);
            Assert.Null(supply[0].Worked);
        }

        [Fact]
        public void SupplyZone_TestedAndWorked_FreshnessTestedWorkedTrue()
        {
            // Base: 1.0150–1.0250, width=0.0100. Worked threshold: L <= 1.0150 - 0.0200 = 0.9950
            var zones = BuildSupplyZone(
                // Wick enters zone (H=1.0160 >= BaseRangeLow=1.0150) but stays below top
                C("2025-01-01T07:00:00Z", 1.0035, 1.0160, 1.0035, 1.0100),
                // Price drops 2x base width below zone bottom
                C("2025-01-01T08:00:00Z", 1.0100, 1.0100, 0.9940, 0.9940)
            );

            var supply = zones.Where(z => z.Type == ZoneType.Supply).ToList();
            Assert.Single(supply);
            Assert.Equal(ZoneFreshness.Tested, supply[0].Freshness);
            Assert.True(supply[0].Worked);
        }

        [Fact]
        public void SupplyZone_TestedNotWorked_FreshnessTestedWorkedFalse()
        {
            // Base: 1.0150–1.0250, width=0.0100. Worked threshold: L <= 0.9950
            var zones = BuildSupplyZone(
                // Wick enters zone
                C("2025-01-01T07:00:00Z", 1.0035, 1.0160, 1.0035, 1.0100),
                // Price drops but not enough (L=1.0000 > 0.9950)
                C("2025-01-01T08:00:00Z", 1.0100, 1.0100, 1.0000, 1.0000)
            );

            var supply = zones.Where(z => z.Type == ZoneType.Supply).ToList();
            Assert.Single(supply);
            Assert.Equal(ZoneFreshness.Tested, supply[0].Freshness);
            Assert.False(supply[0].Worked);
        }

        [Fact]
        public void SupplyZone_Broken_FreshnessBrokenWorkedFalse()
        {
            // Wick pierces above zone top (H > 1.0250)
            var zones = BuildSupplyZone(
                C("2025-01-01T07:00:00Z", 1.0035, 1.0260, 1.0035, 1.0260)
            );

            var supply = zones.Where(z => z.Type == ZoneType.Supply).ToList();
            Assert.Single(supply);
            Assert.Equal(ZoneFreshness.Broken, supply[0].Freshness);
            Assert.False(supply[0].Worked);
        }

        // ===== DEMAND ZONE TESTS =====

        [Fact]
        public void DemandZone_Untested_FreshnessUntestedWorkedNull()
        {
            // Price stays far above zone after formation
            var zones = BuildDemandZone(
                C("2025-01-01T07:00:00Z", 1.0335, 1.0345, 1.0325, 1.0340),
                C("2025-01-01T08:00:00Z", 1.0340, 1.0350, 1.0330, 1.0345)
            );

            var demand = zones.Where(z => z.Type == ZoneType.Demand).ToList();
            Assert.Single(demand);
            Assert.Equal(ZoneFreshness.Untested, demand[0].Freshness);
            Assert.Null(demand[0].Worked);
        }

        [Fact]
        public void DemandZone_TestedAndWorked_FreshnessTestedWorkedTrue()
        {
            // Base: 1.0100–1.0200, width=0.0100. Worked threshold: H >= 1.0200 + 0.0200 = 1.0400
            var zones = BuildDemandZone(
                // Wick enters zone (L=1.0190 <= BaseRangeHigh=1.0200) but stays above bottom
                C("2025-01-01T07:00:00Z", 1.0300, 1.0300, 1.0190, 1.0250),
                // Price rallies 2x base width above zone top
                C("2025-01-01T08:00:00Z", 1.0250, 1.0410, 1.0250, 1.0410)
            );

            var demand = zones.Where(z => z.Type == ZoneType.Demand).ToList();
            Assert.Single(demand);
            Assert.Equal(ZoneFreshness.Tested, demand[0].Freshness);
            Assert.True(demand[0].Worked);
        }

        [Fact]
        public void DemandZone_Broken_FreshnessBrokenWorkedFalse()
        {
            // Wick pierces below zone bottom (L < 1.0100)
            var zones = BuildDemandZone(
                C("2025-01-01T07:00:00Z", 1.0300, 1.0300, 1.0090, 1.0200)
            );

            var demand = zones.Where(z => z.Type == ZoneType.Demand).ToList();
            Assert.Single(demand);
            Assert.Equal(ZoneFreshness.Broken, demand[0].Freshness);
            Assert.False(demand[0].Worked);
        }
    }
}
