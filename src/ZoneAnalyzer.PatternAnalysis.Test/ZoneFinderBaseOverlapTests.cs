using System.Collections.Generic;
using System.Linq;
using Xunit;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis.Test
{
    public class ZoneFinderBaseOverlapTests
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
        public void ExcitingCandle_MostlyInsideBase_AbsorbedIntoBase()
        {
            // Rally leg in, then 3 boring candles forming base at 1.0150–1.0250
            // Then an exciting rally candle whose range is 75%+ inside the base
            // It should be absorbed, so no zone is emitted until a real leg out arrives
            var candles = new List<Candlestick>
            {
                // Leg in: exciting rally
                C("2025-01-01T01:00:00Z", 1.0000, 1.0150, 1.0000, 1.0150),
                // Base: 3 boring candles
                C("2025-01-01T02:00:00Z", 1.0160, 1.0250, 1.0150, 1.0170),
                C("2025-01-01T03:00:00Z", 1.0170, 1.0240, 1.0160, 1.0180),
                C("2025-01-01T04:00:00Z", 1.0180, 1.0230, 1.0170, 1.0190),
                // Exciting rally but 75%+ overlaps base (range 1.0160–1.0280, base 1.0150–1.0250)
                // Overlap = min(1.0280,1.0250)-max(1.0160,1.0150) = 1.0250-1.0160 = 0.0090
                // Candle range = 0.0120, overlap ratio = 0.0090/0.0120 = 75% → absorbed
                C("2025-01-01T05:00:00Z", 1.0160, 1.0280, 1.0160, 1.0280),
                // Now a real leg out drop that breaks away from base
                C("2025-01-01T06:00:00Z", 1.0200, 1.0200, 1.0000, 1.0000),
                // Trigger zone emission
                C("2025-01-01T07:00:00Z", 1.0000, 1.0010, 0.9990, 1.0005),
            };

            var zones = FindZones(candles);
            var supply = zones.Where(z => z.Type == ZoneType.Supply).ToList();
            Assert.Single(supply);
            // The absorbed candle should have expanded the base and increased count
            Assert.Equal(4, supply[0].BaseCandleCount);
            // Base high should have expanded to the absorbed candle's high
            Assert.Equal(1.0280, supply[0].BaseRangeHigh);
        }

        [Fact]
        public void ExcitingCandle_MostlyOutsideBase_StartsLegOut()
        {
            // Same base, but exciting candle has < 75% overlap → starts leg out
            var candles = new List<Candlestick>
            {
                // Leg in: exciting rally
                C("2025-01-01T01:00:00Z", 1.0000, 1.0150, 1.0000, 1.0150),
                // Base: 3 boring candles (base 1.0150–1.0250)
                C("2025-01-01T02:00:00Z", 1.0160, 1.0250, 1.0150, 1.0170),
                C("2025-01-01T03:00:00Z", 1.0170, 1.0240, 1.0160, 1.0180),
                C("2025-01-01T04:00:00Z", 1.0180, 1.0230, 1.0170, 1.0190),
                // Exciting drop mostly outside base (range 1.0000–1.0180)
                // Overlap = min(1.0180,1.0250)-max(1.0000,1.0150) = 1.0180-1.0150 = 0.0030
                // Candle range = 0.0180, overlap ratio = 0.0030/0.0180 = 16.7% → leg out
                C("2025-01-01T05:00:00Z", 1.0180, 1.0180, 1.0000, 1.0000),
                // Trigger zone emission
                C("2025-01-01T06:00:00Z", 1.0000, 1.0010, 0.9990, 1.0005),
            };

            var zones = FindZones(candles);
            var supply = zones.Where(z => z.Type == ZoneType.Supply).ToList();
            Assert.Single(supply);
            // Base should be the original 3 candles, not absorbed
            Assert.Equal(3, supply[0].BaseCandleCount);
            Assert.Equal(1.0250, supply[0].BaseRangeHigh);
        }

        [Fact]
        public void ExcitingCandle_ExactlyAtThreshold_AbsorbedIntoBase()
        {
            // Exciting candle with exactly 75% overlap should be absorbed
            var candles = new List<Candlestick>
            {
                // Leg in: exciting drop
                C("2025-01-01T01:00:00Z", 1.0300, 1.0300, 1.0150, 1.0150),
                // Base: 3 boring candles (base 1.0100–1.0200)
                C("2025-01-01T02:00:00Z", 1.0160, 1.0200, 1.0100, 1.0170),
                C("2025-01-01T03:00:00Z", 1.0170, 1.0190, 1.0110, 1.0180),
                C("2025-01-01T04:00:00Z", 1.0180, 1.0185, 1.0120, 1.0175),
                // Exciting drop: range 1.0125–1.0025, overlap with base 1.0100–1.0200
                // Overlap = min(1.0125,1.0200)-max(1.0025,1.0100) = 1.0125-1.0100 = 0.0025
                // Candle range = 0.0100, overlap ratio = 0.0025/0.0100 = 25% → NOT absorbed
                // Let me recalculate for exactly 75%:
                // Need: overlap/range = 0.75, range = H-L
                // Base 1.0100–1.0200. Candle H=1.0200, L=1.0100 → 100% overlap, too much
                // Candle H=1.0175, L=1.0075 → overlap = 1.0175-1.0100 = 0.0075, range=0.0100 → 75% ✓
                C("2025-01-01T05:00:00Z", 1.0175, 1.0175, 1.0075, 1.0075),
                // Leg out rally to emit the zone
                C("2025-01-01T06:00:00Z", 1.0100, 1.0300, 1.0100, 1.0300),
                // Trigger emission
                C("2025-01-01T07:00:00Z", 1.0300, 1.0310, 1.0290, 1.0305),
            };

            var zones = FindZones(candles);
            var demand = zones.Where(z => z.Type == ZoneType.Demand).ToList();
            Assert.Single(demand);
            // Absorbed: base count should be 4
            Assert.Equal(4, demand[0].BaseCandleCount);
            // Base low expanded to absorbed candle's low
            Assert.Equal(1.0075, demand[0].BaseRangeLow);
        }
    }
}
