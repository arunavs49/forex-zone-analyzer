using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;
using Newtonsoft.Json;

namespace ZoneAnalyzer.PatternAnalysis
{
    public class ZoneManager
    {
        private IEnumerable<Zone> supplyZones;
        private IEnumerable<Zone> demandZones;

        private readonly ZoneFinder zoneFinder;

        public static ZoneManager Create(IEnumerable<Candlestick> candlesticks)
        {
            ZoneFinder zoneFinder = ZoneFinder.Create(candlesticks.ToList().OrderBy(c => DateTime.Parse(c.Time)));
            ZoneManager result = new ZoneManager(zoneFinder);
            result.PopulateZones();
            return result;
        }

        private ZoneManager(ZoneFinder zoneFinder)
        {
            this.zoneFinder = zoneFinder;
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
            List<Zone> zones = this.zoneFinder.GetAllZones();

            // TODO: Add condition for zone being on a side of current price and not being tested as well
            this.supplyZones = zones.Where(zone => zone.Type == ZoneType.Supply);
            this.demandZones = zones.Where(zone => zone.Type == ZoneType.Demand);
        }
    }
}
