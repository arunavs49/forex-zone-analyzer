using System;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis
{
    public class ZoneConfiguration
    {
        public int MinBaseLength { get; set; } = 1;

        public double MinLegInToBaseRangeRatio { get; set; } = 1;

        public double MinLegOutToBaseRangeRatio { get; set; } = 1;

        public bool IsMatch(Zone zone)
        {
            if (zone.BaseCandleCount >= MinBaseLength &&
                Math.Abs((zone.LegInEndPrice - zone.LegInStartPrice) / (zone.BaseRangeHigh - zone.BaseRangeLow)) >= MinLegOutToBaseRangeRatio &&
                Math.Abs((zone.LegOutEndPrice - zone.LegOutStartPrice) / (zone.BaseRangeHigh - zone.BaseRangeLow)) >= MinLegOutToBaseRangeRatio)
            {
                return true;
            }

            return false;
        }
    }
}
