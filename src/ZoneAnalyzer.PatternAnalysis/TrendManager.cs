using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis
{
    public enum TrendDirection
    {
        Up,
        Down,
        Sideways
    }

    public class TrendManager
    {
        private readonly List<Candlestick> _candlesticks;
        private readonly TrendConfiguration _config;

        public static TrendManager Create(IEnumerable<Candlestick> candlesticks, TrendConfiguration? config = null)
        {
            var sorted = candlesticks.ToList()
                .OrderBy(c => DateTime.Parse(c.Time, CultureInfo.InvariantCulture))
                .ToList();
            return new TrendManager(sorted, config ?? new TrendConfiguration());
        }

        private TrendManager(List<Candlestick> candlesticks, TrendConfiguration config)
        {
            _candlesticks = candlesticks;
            _config = config;
        }

        public string GetTrend(DateTime? currentTimeForTrend = null)
        {
            return GetTrendDirection(currentTimeForTrend).ToString();
        }

        public TrendDirection GetTrendDirection(DateTime? currentTimeForTrend = null)
        {
            var candles = GetRelevantCandles(currentTimeForTrend);
            if (candles.Count < _config.SwingLookback * 2 + 1)
                return TrendDirection.Sideways;

            var swingHighs = FindSwingHighs(candles, _config.SwingLookback);
            var swingLows = FindSwingLows(candles, _config.SwingLookback);

            if (swingHighs.Count < _config.MinSwingPoints || swingLows.Count < _config.MinSwingPoints)
                return TrendDirection.Sideways;

            // Compare the last two swing highs and last two swing lows
            var lastHighs = swingHighs.TakeLast(2).ToArray();
            var lastLows = swingLows.TakeLast(2).ToArray();

            bool higherHigh = lastHighs[1].Price > lastHighs[0].Price;
            bool higherLow = lastLows[1].Price > lastLows[0].Price;
            bool lowerHigh = lastHighs[1].Price < lastHighs[0].Price;
            bool lowerLow = lastLows[1].Price < lastLows[0].Price;

            if (higherHigh && higherLow)
                return TrendDirection.Up;
            if (lowerHigh && lowerLow)
                return TrendDirection.Down;

            return TrendDirection.Sideways;
        }

        /// <summary>
        /// Returns identified swing highs for diagnostic/display purposes.
        /// </summary>
        public List<SwingPoint> GetSwingHighs(DateTime? currentTimeForTrend = null)
        {
            var candles = GetRelevantCandles(currentTimeForTrend);
            return FindSwingHighs(candles, _config.SwingLookback);
        }

        /// <summary>
        /// Returns identified swing lows for diagnostic/display purposes.
        /// </summary>
        public List<SwingPoint> GetSwingLows(DateTime? currentTimeForTrend = null)
        {
            var candles = GetRelevantCandles(currentTimeForTrend);
            return FindSwingLows(candles, _config.SwingLookback);
        }

        private List<Candlestick> GetRelevantCandles(DateTime? currentTimeForTrend)
        {
            IEnumerable<Candlestick> filtered = _candlesticks;

            if (currentTimeForTrend.HasValue)
            {
                filtered = _candlesticks.Where(c =>
                    DateTime.Parse(c.Time, CultureInfo.InvariantCulture) <= currentTimeForTrend.Value);
            }

            return filtered.TakeLast(_config.TrendCandleCount).ToList();
        }

        internal static List<SwingPoint> FindSwingHighs(List<Candlestick> candles, int lookback)
        {
            var swingHighs = new List<SwingPoint>();

            for (int i = lookback; i < candles.Count - lookback; i++)
            {
                var high = candles[i].Mid.H;
                bool isSwingHigh = true;

                for (int j = 1; j <= lookback; j++)
                {
                    if (candles[i - j].Mid.H > high || candles[i + j].Mid.H > high)
                    {
                        isSwingHigh = false;
                        break;
                    }
                }

                if (isSwingHigh)
                {
                    swingHighs.Add(new SwingPoint
                    {
                        Index = i,
                        Price = high,
                        Time = DateTime.Parse(candles[i].Time, CultureInfo.InvariantCulture)
                    });
                }
            }

            return swingHighs;
        }

        internal static List<SwingPoint> FindSwingLows(List<Candlestick> candles, int lookback)
        {
            var swingLows = new List<SwingPoint>();

            for (int i = lookback; i < candles.Count - lookback; i++)
            {
                var low = candles[i].Mid.L;
                bool isSwingLow = true;

                for (int j = 1; j <= lookback; j++)
                {
                    if (candles[i - j].Mid.L < low || candles[i + j].Mid.L < low)
                    {
                        isSwingLow = false;
                        break;
                    }
                }

                if (isSwingLow)
                {
                    swingLows.Add(new SwingPoint
                    {
                        Index = i,
                        Price = low,
                        Time = DateTime.Parse(candles[i].Time, CultureInfo.InvariantCulture)
                    });
                }
            }

            return swingLows;
        }
    }

    public class SwingPoint
    {
        public int Index { get; set; }
        public double Price { get; set; }
        public DateTime Time { get; set; }
    }
}
