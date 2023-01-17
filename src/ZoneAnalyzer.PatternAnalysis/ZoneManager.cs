using System;
using System.Collections.Generic;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis
{
    public class ZoneManager
    {
        private List<Zone> supplyZones;
        private List<Zone> demandZones;

        private readonly IEnumerable<Candlestick> candlesticks;

        public static ZoneManager Create(IEnumerable<Candlestick> candlesticks)
        {
            ZoneManager result = new ZoneManager(candlesticks);
            result.PopulateZones();
            return result;
        }

        private ZoneManager(IEnumerable<Candlestick> candlesticks)
        {
            this.candlesticks = candlesticks;
        }

        public List<Zone> GetSupplyZones()
        {
            return this.supplyZones.ToList();
        }

        public List<Zone> GetDemandZones()
        {
            return this.demandZones.ToList();
        }

        private void PopulateZones()
        {
            this.supplyZones = FindSupplyZones(this.candlesticks);
            this.demandZones = FindDemandZones(this.candlesticks);
        }

        private static List<Zone> FindDemandZones(IEnumerable<Candlestick> candlesticks)
        {
            return new List<Zone>()
            {
                new Zone()
                {
                    Type = ZoneType.Supply,
                    StartTime = DateTime.Now,
                    EndTime= DateTime.Now,
                    LegInStartPrice = 1.2,
                    LegInEndPrice = 1.1,
                    BaseRangeHigh = 1.14,
                    BaseRangeLow = 1.07,
                    BaseCandleCount = 4,
                    LegOutStartPrice= 1.08,
                    LegOutEndPrice = 0.98
                }
            };
        }

        private static List<Zone> FindSupplyZones(IEnumerable<Candlestick> candlesticks)
        {
            return new List<Zone>()
            {
                new Zone()
                {
                    Type = ZoneType.Demand,
                    StartTime = DateTime.Now,
                    EndTime= DateTime.Now,
                    LegOutEndPrice = 1.2,
                    LegOutStartPrice = 1.1,
                    BaseRangeHigh = 1.14,
                    BaseRangeLow = 1.07,
                    BaseCandleCount = 4,
                    LegInEndPrice= 1.08,
                    LegInStartPrice = 0.98
                }
            };
        }
    }
}
