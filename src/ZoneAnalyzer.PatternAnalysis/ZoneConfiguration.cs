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

            // Leg length = total leg movement minus the portion overlapping the base
            double legInTop, legInBottom;
            if (zone.LegInEndPrice > zone.LegInStartPrice) // Rally leg in
            {
                legInBottom = zone.LegInStartPrice;
                legInTop = zone.LegInEndPrice;
            }
            else // Drop leg in
            {
                legInBottom = zone.LegInEndPrice;
                legInTop = zone.LegInStartPrice;
            }

            double legOutTop, legOutBottom;
            if (zone.LegOutEndPrice > zone.LegOutStartPrice) // Rally leg out
            {
                legOutBottom = zone.LegOutStartPrice;
                legOutTop = zone.LegOutEndPrice;
            }
            else // Drop leg out
            {
                legOutBottom = zone.LegOutEndPrice;
                legOutTop = zone.LegOutStartPrice;
            }

            var legInTotal = legInTop - legInBottom;
            var legInOverlap = Math.Max(0, Math.Min(legInTop, zone.BaseRangeHigh) - Math.Max(legInBottom, zone.BaseRangeLow));
            var legInLength = legInTotal - legInOverlap;

            var legOutTotal = legOutTop - legOutBottom;
            var legOutOverlap = Math.Max(0, Math.Min(legOutTop, zone.BaseRangeHigh) - Math.Max(legOutBottom, zone.BaseRangeLow));
            var legOutLength = legOutTotal - legOutOverlap;

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
