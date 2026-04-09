using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;
using Newtonsoft.Json;

namespace ZoneAnalyzer.PatternAnalysis
{
    public class ZoneFinder
    {
        private readonly IOrderedEnumerable<Candlestick> candlesticks;

        public static ZoneFinder Create(IOrderedEnumerable<Candlestick> candlesticks)
        {
            ZoneFinder result = new ZoneFinder(candlesticks);
            return result;
        }

        private ZoneFinder(IOrderedEnumerable<Candlestick> candlesticks)
        {
            this.candlesticks = candlesticks;
        }

        //optimize this function
        public List<Zone> GetAllZones()
        {
            var zones = new List<Zone>();

            //find all zones in the data using candlestick.GetShape()
            var zoneBuildingState = ZoneBuildingState.NotStarted;
            var legType = LegType.Rally;
            var legInStartPrice = 0.0;
            var legInEndPrice = 0.0;
            var legOutStartPrice = 0.0;
            var legOutEndPrice = 0.0;
            var baseRangeHigh = 0.0;
            var baseRangeLow = 0.0;
            var baseCandleCount = 0;
            var zoneStartTime = DateTime.Now;
            var zoneEndTime = DateTime.Now;
            var zoneType = ZoneType.Supply;

            var legStartTime = DateTime.Now;

            foreach (var candlestick in candlesticks)
            {
                var candlestickShape = candlestick.GetShape();
                switch (zoneBuildingState)
                {
                    case ZoneBuildingState.NotStarted:
                        if (candlestickShape == CandlestickShape.ExcitingRally)
                        {
                            zoneBuildingState = ZoneBuildingState.BuildingLegIn;
                            legStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            legType = LegType.Rally;
                            legInStartPrice = candlestick.GetCandlestickData().L;
                            legInEndPrice = candlestick.GetCandlestickData().H;
                            zoneStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                        }
                        else if (candlestickShape == CandlestickShape.ExcitingDrop)
                        {
                            zoneBuildingState = ZoneBuildingState.BuildingLegIn;
                            legStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            legType = LegType.Drop;
                            legInStartPrice = candlestick.GetCandlestickData().H;
                            legInEndPrice = candlestick.GetCandlestickData().L;
                            zoneStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                        }
                        break;

                    case ZoneBuildingState.BuildingLegIn:
                        if (legType == LegType.Rally)
                        {
                            if (candlestickShape == CandlestickShape.ExcitingRally)
                            {
                                legInEndPrice = candlestick.GetCandlestickData().H;
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                            else if (candlestickShape == CandlestickShape.Boring)
                            {
                                zoneBuildingState = ZoneBuildingState.BuildingBase;
                                baseRangeHigh = candlestick.GetCandlestickData().H;
                                baseRangeLow = candlestick.GetCandlestickData().L;
                                baseCandleCount = 1;
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                            else if (candlestickShape == CandlestickShape.ExcitingDrop)
                            {
                                zoneBuildingState = ZoneBuildingState.BuildingLegIn;
                                legType = LegType.Drop;
                                legInStartPrice = candlestick.GetCandlestickData().H;
                                legInEndPrice = candlestick.GetCandlestickData().L;
                                zoneStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                        }
                        else if (legType == LegType.Drop)
                        {
                            if (candlestickShape == CandlestickShape.ExcitingDrop)
                            {
                                legInEndPrice = candlestick.GetCandlestickData().L;
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                            else if (candlestickShape == CandlestickShape.Boring)
                            {
                                zoneBuildingState = ZoneBuildingState.BuildingBase;
                                baseRangeHigh = candlestick.GetCandlestickData().H;
                                baseRangeLow = candlestick.GetCandlestickData().L;
                                baseCandleCount = 1;
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                            else if (candlestickShape == CandlestickShape.ExcitingRally)
                            {
                                zoneBuildingState = ZoneBuildingState.BuildingLegIn;
                                legType = LegType.Rally;
                                legInStartPrice = candlestick.GetCandlestickData().L;
                                legInEndPrice = candlestick.GetCandlestickData().H;
                                zoneStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                        }
                        break;

                    case ZoneBuildingState.BuildingBase:
                        if (candlestickShape == CandlestickShape.Boring)
                        {
                            // add to base
                            if (candlestick.GetCandlestickData().H > baseRangeHigh)
                            {
                                baseRangeHigh = candlestick.GetCandlestickData().H;
                            }
                            if (candlestick.GetCandlestickData().L < baseRangeLow)
                            {
                                baseRangeLow = candlestick.GetCandlestickData().L;
                            }
                            baseCandleCount++;
                            zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                        }
                        else if (candlestickShape == CandlestickShape.ExcitingRally
                              || candlestickShape == CandlestickShape.ExcitingDrop)
                        {
                            var candleH = candlestick.GetCandlestickData().H;
                            var candleL = candlestick.GetCandlestickData().L;

                            if (OverlapsWithBase(candleH, candleL, baseRangeHigh, baseRangeLow))
                            {
                                // Exciting candle mostly within base — absorb it
                                if (candleH > baseRangeHigh) baseRangeHigh = candleH;
                                if (candleL < baseRangeLow) baseRangeLow = candleL;
                                baseCandleCount++;
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                            else if (candlestickShape == CandlestickShape.ExcitingRally)
                            {
                                zoneBuildingState = ZoneBuildingState.BuildingLegOut;
                                zoneType = ZoneType.Demand;
                                legOutStartPrice = candleL;
                                legOutEndPrice = candleH;
                                legStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                legType = LegType.Rally;
                            }
                            else // ExcitingDrop
                            {
                                zoneBuildingState = ZoneBuildingState.BuildingLegOut;
                                zoneType = ZoneType.Supply;
                                legOutStartPrice = candleH;
                                legOutEndPrice = candleL;
                                legStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                legType = LegType.Drop;
                            }
                        }

                        break;

                    case ZoneBuildingState.BuildingLegOut:
                        if (legType == LegType.Rally)
                        {
                            if (candlestickShape == CandlestickShape.ExcitingRally)
                            {
                                legOutEndPrice = candlestick.GetCandlestickData().H;
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                zoneBuildingState = ZoneBuildingState.NotStarted;
                                Zone zone = new Zone();
                                zone.Type = zoneType;
                                zone.StartTime = zoneStartTime;
                                zone.EndTime = zoneEndTime;
                                zone.LegInStartPrice = legInStartPrice;
                                zone.LegInEndPrice = legInEndPrice;
                                zone.BaseRangeHigh = baseRangeHigh;
                                zone.BaseRangeLow = baseRangeLow;
                                zone.BaseCandleCount = baseCandleCount;
                                zone.LegOutStartPrice = legOutStartPrice;
                                zone.LegOutEndPrice = legOutEndPrice;
                                zones.Add(zone);

                                // if  this candlestick is a boring candlestick, start a new zone with the current leg out start price as the leg in start price
                                if (candlestickShape == CandlestickShape.Boring)
                                {
                                    zoneStartTime = legStartTime;
                                    legInStartPrice = legOutStartPrice;
                                    legInEndPrice = legOutEndPrice;

                                    zoneBuildingState = ZoneBuildingState.BuildingBase;
                                    baseRangeHigh = candlestick.GetCandlestickData().H;
                                    baseRangeLow = candlestick.GetCandlestickData().L;
                                    baseCandleCount = 1;
                                    zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                }
                                else if (candlestickShape == CandlestickShape.ExcitingDrop)
                                {
                                    zoneBuildingState = ZoneBuildingState.BuildingLegIn;
                                    legType = LegType.Drop;
                                    legInStartPrice = candlestick.GetCandlestickData().H;
                                    legInEndPrice = candlestick.GetCandlestickData().L;
                                    zoneStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                    zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                }
                            }
                        }
                        else if (legType == LegType.Drop)
                        {
                            if (candlestickShape == CandlestickShape.ExcitingDrop)
                            {
                                legOutEndPrice = candlestick.GetCandlestickData().L;
                                zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                zoneBuildingState = ZoneBuildingState.NotStarted;
                                Zone zone = new Zone();
                                zone.Type = zoneType;
                                zone.StartTime = zoneStartTime;
                                zone.EndTime = zoneEndTime;
                                zone.LegInStartPrice = legInStartPrice;
                                zone.LegInEndPrice = legInEndPrice;
                                zone.BaseRangeHigh = baseRangeHigh;
                                zone.BaseRangeLow = baseRangeLow;
                                zone.BaseCandleCount = baseCandleCount;
                                zone.LegOutStartPrice = legOutStartPrice;
                                zone.LegOutEndPrice = legOutEndPrice;
                                zones.Add(zone);

                                // if  this candlestick is a boring candlestick, start a new zone with the current leg out start price as the leg in start price
                                if (candlestickShape == CandlestickShape.Boring)
                                {
                                    zoneStartTime = legStartTime;
                                    legInStartPrice = legOutStartPrice;
                                    legInEndPrice = legOutEndPrice;
                                    zoneBuildingState = ZoneBuildingState.BuildingBase;
                                    baseRangeHigh = candlestick.GetCandlestickData().H;
                                    baseRangeLow = candlestick.GetCandlestickData().L;
                                    baseCandleCount = 1;
                                    zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                }
                                else if (candlestickShape == CandlestickShape.ExcitingRally)
                                {
                                    zoneBuildingState = ZoneBuildingState.BuildingLegIn;
                                    legType = LegType.Rally;
                                    legInStartPrice = candlestick.GetCandlestickData().L;
                                    legInEndPrice = candlestick.GetCandlestickData().H;
                                    zoneStartTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                    zoneEndTime = DateTime.Parse(candlestick.Time, CultureInfo.InvariantCulture);
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("LegType not set");
                        }

                        break;
                }
            }

            // Second pass: evaluate freshness for each zone
            var candlestickList = candlesticks.ToList();
            foreach (var zone in zones)
            {
                EvaluateZoneStatus(zone, candlestickList);
            }

            return zones;
        }

        private static void EvaluateZoneStatus(Zone zone, List<Candlestick> candlestickList)
        {
            var freshness = ZoneFreshness.Untested;
            var baseWidth = zone.BaseRangeHigh - zone.BaseRangeLow;
            var tested = false;
            var worked = false;

            foreach (var candle in candlestickList)
            {
                var candleTime = DateTime.Parse(candle.Time, CultureInfo.InvariantCulture);
                if (candleTime <= zone.EndTime)
                    continue;

                var data = candle.GetCandlestickData();

                if (zone.Type == ZoneType.Supply)
                {
                    // Supply: sellers at the zone. Price dropped away.
                    // Broken if wick pierces above zone top.
                    // Tested if wick enters zone but stays below zone top.
                    if (data.H > zone.BaseRangeHigh)
                    {
                        freshness = ZoneFreshness.Broken;
                        break;
                    }
                    if (data.H >= zone.BaseRangeLow)
                    {
                        freshness = ZoneFreshness.Tested;
                        tested = true;
                    }
                    // After tested, check if price dropped 2x base width from zone bottom
                    if (tested && data.L <= zone.BaseRangeLow - 2 * baseWidth)
                    {
                        worked = true;
                    }
                }
                else // Demand
                {
                    // Demand: buyers at the zone. Price rallied away.
                    // Broken if wick pierces below zone bottom.
                    // Tested if wick enters zone but stays above zone bottom.
                    if (data.L < zone.BaseRangeLow)
                    {
                        freshness = ZoneFreshness.Broken;
                        break;
                    }
                    if (data.L <= zone.BaseRangeHigh)
                    {
                        freshness = ZoneFreshness.Tested;
                        tested = true;
                    }
                    // After tested, check if price rallied 2x base width from zone top
                    if (tested && data.H >= zone.BaseRangeHigh + 2 * baseWidth)
                    {
                        worked = true;
                    }
                }
            }

            zone.Freshness = freshness;
            zone.Worked = freshness == ZoneFreshness.Untested ? null : (bool?)worked;
        }

        private const double BaseOverlapThreshold = 0.75;
        private const double FloatTolerance = 1e-9;

        private static bool OverlapsWithBase(double candleH, double candleL, double baseHigh, double baseLow)
        {
            var candleRange = candleH - candleL;
            if (candleRange <= 0) return true;

            var overlapHigh = Math.Min(candleH, baseHigh);
            var overlapLow = Math.Max(candleL, baseLow);
            var overlap = Math.Max(0, overlapHigh - overlapLow);

            return overlap / candleRange >= BaseOverlapThreshold - FloatTolerance;
        }

        private enum ZoneBuildingState
        {
            NotStarted,
            BuildingLegIn,
            BuildingBase,
            BuildingLegOut
        }

        private enum LegType
        {
            Rally,
            Drop
        }
    }
}
