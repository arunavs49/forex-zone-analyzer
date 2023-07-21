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
        private readonly ZoneConfiguration configuration;

        public static ZoneManager Create(IEnumerable<Candlestick> candlesticks, ZoneConfiguration configuration = null)
        {
            ZoneFinder zoneFinder = ZoneFinder.Create(candlesticks.ToList().OrderBy(c => DateTime.Parse(c.Time)));
            ZoneManager result = new ZoneManager(zoneFinder, configuration);
            result.PopulateZones();
            return result;
        }

        private ZoneManager(ZoneFinder zoneFinder, ZoneConfiguration configuration)
        {
            this.zoneFinder = zoneFinder;
            this.configuration = configuration;
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
            IEnumerable<Zone> zones = this.zoneFinder.GetAllZones().Where(zone  => configuration == null || configuration.IsMatch(zone));

            // TODO: Add condition for zone being on a side of current price and not being tested as well
            this.supplyZones = zones.Where(zone => zone.Type == ZoneType.Supply);
            this.demandZones = zones.Where(zone => zone.Type == ZoneType.Demand);
        }
    }
}
