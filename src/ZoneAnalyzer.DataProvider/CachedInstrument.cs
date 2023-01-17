using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk;
using GeriRemenyi.Oanda.V20.Sdk.Common.Types;
using GeriRemenyi.Oanda.V20.Sdk.Instrument;

namespace ZoneAnalyzer.DataProvider
{
    internal class CachedInstrument : IInstrument
    {
        /// <summary>
        /// The Oanda API connection
        /// </summary>
        private readonly GeriRemenyi.Oanda.V20.Sdk.Instrument.Instrument _instrument;

        /// <summary>
        /// Instrument constructor to setup the connection and instrument name
        /// </summary>
        /// <param name="connection">The Oanda API connection</param>
        /// <param name="instrument">The instrument name</param>
        public CachedInstrument(IOandaApiConnection connection, InstrumentName instrument)
        {
            _instrument = new GeriRemenyi.Oanda.V20.Sdk.Instrument.Instrument(connection, instrument);
        }

        IEnumerable<Candlestick> IInstrument.GetCandlesByTime(CandlestickGranularity granularity, DateTime from, DateTime to, IEnumerable<PricingComponent> pricingComponents)
        {
            return _instrument.GetCandlesByTime(granularity, from, to, pricingComponents);
        }

        Task<IEnumerable<Candlestick>> IInstrument.GetCandlesByTimeAsync(CandlestickGranularity granularity, DateTime from, DateTime to, IEnumerable<PricingComponent> pricingComponents)
        {
            return _instrument.GetCandlesByTimeAsync(granularity, from, to, pricingComponents);
        }

        IEnumerable<Candlestick> IInstrument.GetLastNCandles(CandlestickGranularity granularity, int n, IEnumerable<PricingComponent> pricingComponents)
        {
            return _instrument.GetLastNCandles(granularity, n, pricingComponents);
        }

        Task<IEnumerable<Candlestick>> IInstrument.GetLastNCandlesAsync(CandlestickGranularity granularity, int n, IEnumerable<PricingComponent> pricingComponents)
        {
            return _instrument.GetLastNCandlesAsync(granularity, n, pricingComponents);
        }
    }
}
