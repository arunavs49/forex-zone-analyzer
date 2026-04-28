using System;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis
{
    public class ZoneConfiguration
    {
        public int MinBaseLength { get; set; } = 1;

        public int MaxBaseLength { get; set; } = 6;

        public double MinLegInToBaseRangeRatio { get; set; } = 1;

        public double MinLegOutToBaseRangeRatio { get; set; } = 1;

        public bool IsMatch(Zone zone)
        {
            var baseRange = zone.BaseRangeHigh - zone.BaseRangeLow;
            if (baseRange <= 0)
                return false;

            // Leg length = movement OUTSIDE the base (from leg extreme to near base edge)
            double legInLength;
            if (zone.LegInEndPrice > zone.LegInStartPrice) // Rally leg in (enters base from below)
                legInLength = zone.BaseRangeLow - zone.LegInStartPrice;
            else // Drop leg in (enters base from above)
                legInLength = zone.LegInStartPrice - zone.BaseRangeHigh;

            double legOutLength;
            if (zone.LegOutEndPrice > zone.LegOutStartPrice) // Rally leg out (exits from top)
                legOutLength = zone.LegOutEndPrice - zone.BaseRangeHigh;
            else // Drop leg out (exits from bottom)
                legOutLength = zone.BaseRangeLow - zone.LegOutEndPrice;

            if (legInLength <= 0 || legOutLength <= 0)
                return false;

            var legInRatio = legInLength / baseRange;
            var legOutRatio = legOutLength / baseRange;

            return zone.BaseCandleCount >= MinBaseLength &&
                   zone.BaseCandleCount <= MaxBaseLength &&
                   legInRatio >= MinLegInToBaseRangeRatio &&
                   legOutRatio >= MinLegOutToBaseRangeRatio;
        }
    }
}
