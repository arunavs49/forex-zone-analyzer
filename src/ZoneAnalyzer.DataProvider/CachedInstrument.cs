using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;
using GeriRemenyi.Oanda.V20.Sdk.Instrument;

namespace ZoneAnalyzer.DataProvider
{
    internal class CachedInstrument : IInstrument
    {
        IEnumerable<Candlestick> IInstrument.GetCandlesByTime(CandlestickGranularity granularity, DateTime from, DateTime to, IEnumerable<PricingComponent> pricingComponents)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<Candlestick>> IInstrument.GetCandlesByTimeAsync(CandlestickGranularity granularity, DateTime from, DateTime to, IEnumerable<PricingComponent> pricingComponents)
        {
            throw new NotImplementedException();
        }

        IEnumerable<Candlestick> IInstrument.GetLastNCandles(CandlestickGranularity granularity, int n, IEnumerable<PricingComponent> pricingComponents)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<Candlestick>> IInstrument.GetLastNCandlesAsync(CandlestickGranularity granularity, int n, IEnumerable<PricingComponent> pricingComponents)
        {
            throw new NotImplementedException();
        }
    }
}
