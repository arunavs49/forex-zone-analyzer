using GeriRemenyi.Oanda.V20.Client.Model;
using System;

namespace ZoneAnalyzer.PatternAnalysis
{
    internal static class CandlestickExtensions
    {
        internal static CandlestickData GetCandlestickData(this Candlestick candlestick, CandlestickType candlestickType = CandlestickType.Mid)
        {
            CandlestickData result = null;
            switch (candlestickType)
            {
                case CandlestickType.Ask:
                    result = candlestick.Ask;
                    break;
                case CandlestickType.Mid:
                    result = candlestick.Mid;
                    break;
                case CandlestickType.Bid:
                    result = candlestick.Bid;
                    break;
            }

            return result;
        }

        internal static CandlestickShape GetShape(this Candlestick candlestick, CandlestickType candlestickType = CandlestickType.Mid)
        {
            CandlestickShape result = CandlestickShape.Boring;
            CandlestickData candlestickData = candlestick.GetCandlestickData(candlestickType);
            var range = candlestickData.H - candlestickData.L;
            if (range > 0 && Math.Abs(candlestickData.O - candlestickData.C) * 2 >= range)
            {
                if (candlestickData.O - candlestickData.C > 0)
                    result = CandlestickShape.ExcitingDrop;
                else
                    result = CandlestickShape.ExcitingRally;
            }

            return result;
        }
    }
}
