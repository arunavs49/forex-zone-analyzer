using System;
using System.Collections.Generic;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;
using MathNet.Numerics;

namespace ZoneAnalyzer.PatternAnalysis
{
    public class TrendManager
    {
        private readonly IOrderedEnumerable<Candlestick> candlesticks;
        public static TrendManager Create(IEnumerable<Candlestick> candlesticks)
        {
            TrendManager result = new TrendManager(candlesticks.ToList().OrderBy(c => DateTime.Parse(c.Time)));
            return result;
        }

        private TrendManager(IOrderedEnumerable<Candlestick> candlesticks)
        {
            this.candlesticks = candlesticks;
        }

        public string GetTrend(DateTime? currentTimeForTrend = null)
        {
            if (!currentTimeForTrend.HasValue)
            {
                currentTimeForTrend = candlesticks.Max(c => DateTime.Parse(c.Time));
            }

            // Determine if the trend is Up, Down or Sideways using the Close price of the candlesticks.
            IOrderedEnumerable<Candlestick> widerSample = this.candlesticks.Where(c => DateTime.Parse(c.Time) <= currentTimeForTrend)
                .OrderBy(c => DateTime.Parse(c.Time));
            double[] trendPrices = widerSample
                .TakeLast(Math.Min(60, widerSample.Count()))
                .OrderBy(c => DateTime.Parse(c.Time))
                .Select(c => c.Mid.C)
                .ToArray();

            return GetTrendDirection(trendPrices).ToString();
        }


        private static Trend GetTrendDirection(double[] data, int degree = 1)
        {
            // data is an array of closing prices
            // degree is the degree of the polynomial to fit

            // create an index array for the data
            double[] index = new double[data.Length];
            for (int i = 0; i < index.Length; i++)
            {
                index[i] = i;
            }

            // fit a polynomial of the given degree to the data
            double[] coeffs = Fit.Polynomial(index, data, degree);

            // get the slope of the polynomial
            double slope = coeffs[1];

            // determine the trend direction based on the slope
            if (slope > 0)
            {
                return Trend.Up;
            }
            else if (slope < 0)
            {
                return Trend.Down;
            }
            else
            {
                return Trend.Sideways;
            }
        }


        private enum Trend
        {
            Up,
            Down,
            Sideways
        };
    }
}
