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

            var legInRatio = Math.Abs((zone.LegInEndPrice - zone.LegInStartPrice) / baseRange);
            var legOutRatio = Math.Abs((zone.LegOutEndPrice - zone.LegOutStartPrice) / baseRange);

            return zone.BaseCandleCount >= MinBaseLength &&
                   zone.BaseCandleCount <= MaxBaseLength &&
                   legInRatio >= MinLegInToBaseRangeRatio &&
                   legOutRatio >= MinLegOutToBaseRangeRatio;
        }
    }
}
