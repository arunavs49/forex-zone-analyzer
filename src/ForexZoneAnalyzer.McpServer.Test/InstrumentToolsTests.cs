using ForexZoneAnalyzer.McpServer.Services;
using ForexZoneAnalyzer.McpServer.Tools;
using GeriRemenyi.Oanda.V20.Client.Model;
using GeriRemenyi.Oanda.V20.Sdk;
using GeriRemenyi.Oanda.V20.Sdk.Account;
using GeriRemenyi.Oanda.V20.Sdk.Instrument;
using GeriRemenyi.Oanda.V20.Sdk.Trade;
using Moq;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.McpServer.Test;

public class InstrumentToolsTests
{
    private readonly Mock<IOandaConnectionService> _connectionServiceMock;
    private readonly Mock<IOandaApiConnection> _connectionMock;
    private readonly Mock<IInstrument> _instrumentMock;
    private readonly Mock<CandleCacheReader> _cacheReaderMock;

    public InstrumentToolsTests()
    {
        _connectionServiceMock = new Mock<IOandaConnectionService>();
        _connectionMock = new Mock<IOandaApiConnection>();
        _instrumentMock = new Mock<IInstrument>();
        _cacheReaderMock = new Mock<CandleCacheReader>(null, null);

        // Cache returns no coverage — all requests fall through to OANDA
        _cacheReaderMock
            .Setup(x => x.GetCoverageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((DateTime, DateTime, int)?)null);

        _connectionServiceMock
            .Setup(x => x.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_connectionMock.Object);

        _connectionMock
            .Setup(x => x.GetInstrument(It.IsAny<InstrumentName>()))
            .Returns(_instrumentMock.Object);
    }

    [Fact]
    public async Task GetCandles_ReturnsFormattedCandles()
    {
        var candles = CreateSampleCandles(5);
        _instrumentMock
            .Setup(x => x.GetLastNCandlesAsync(
                It.IsAny<CandlestickGranularity>(), It.IsAny<int>(),
                It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()))
            .ReturnsAsync(candles);

        var result = await InstrumentTools.GetCandles(
            _connectionServiceMock.Object, _cacheReaderMock.Object, "EUR_USD", "H1", 5);

        Assert.Contains("Open", result);
        Assert.Contains("Close", result);
        var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
        Assert.Equal(5, parsed!.Count);
    }

    [Fact]
    public async Task GetCandles_ClampsCountTo5000()
    {
        var candles = CreateSampleCandles(1);
        _instrumentMock
            .Setup(x => x.GetLastNCandlesAsync(
                It.IsAny<CandlestickGranularity>(), 5000,
                It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()))
            .ReturnsAsync(candles);

        await InstrumentTools.GetCandles(
            _connectionServiceMock.Object, _cacheReaderMock.Object, "EUR_USD", "H1", 10000);

        _instrumentMock.Verify(x => x.GetLastNCandlesAsync(
            CandlestickGranularity.H1, 5000,
            It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()), Times.Once);
    }

    [Fact]
    public async Task GetCandlesByTime_ReturnsCandles()
    {
        var candles = CreateSampleCandles(3);
        _instrumentMock
            .Setup(x => x.GetCandlesByTimeAsync(
                It.IsAny<CandlestickGranularity>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()))
            .ReturnsAsync(candles);

        var result = await InstrumentTools.GetCandlesByTime(
            _connectionServiceMock.Object, "EUR_USD", "D",
            "2025-01-01T00:00:00Z", "2025-03-01T00:00:00Z");

        var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
        Assert.Equal(3, parsed!.Count);
    }

    [Fact]
    public async Task GetSupplyDemandZones_ReturnsZoneAnalysis()
    {
        // Create enough candles with variation to potentially form zones
        var candles = CreateVolatileCandles(100);
        _instrumentMock
            .Setup(x => x.GetLastNCandlesAsync(
                It.IsAny<CandlestickGranularity>(), It.IsAny<int>(),
                It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()))
            .ReturnsAsync(candles);

        var result = await InstrumentTools.GetSupplyDemandZones(
            _connectionServiceMock.Object, "EUR_USD", "H4", 100);

        Assert.Contains("Instrument", result);
        Assert.Contains("EUR_USD", result);
        Assert.Contains("SupplyZones", result);
        Assert.Contains("DemandZones", result);
        Assert.Contains("CandlesAnalyzed", result);
    }

    [Fact]
    public async Task GetTrend_ReturnsTrendDirection()
    {
        var candles = CreateTrendingCandles(100, trending: true);
        _instrumentMock
            .Setup(x => x.GetLastNCandlesAsync(
                It.IsAny<CandlestickGranularity>(), It.IsAny<int>(),
                It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()))
            .ReturnsAsync(candles);

        var result = await InstrumentTools.GetTrend(
            _connectionServiceMock.Object, "EUR_USD", "D", 100);

        Assert.Contains("Trend", result);
        Assert.Contains("EUR_USD", result);
        // Upward trending data should yield "Up"
        Assert.Contains("Up", result);
    }

    [Fact]
    public async Task GetTrend_DetectsDowntrend()
    {
        var candles = CreateTrendingCandles(100, trending: false);
        _instrumentMock
            .Setup(x => x.GetLastNCandlesAsync(
                It.IsAny<CandlestickGranularity>(), It.IsAny<int>(),
                It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()))
            .ReturnsAsync(candles);

        var result = await InstrumentTools.GetTrend(
            _connectionServiceMock.Object, "EUR_USD", "D", 100);

        Assert.Contains("Down", result);
    }

    [Fact]
    public async Task GetCandles_ParsesGranularityCorrectly()
    {
        var candles = CreateSampleCandles(1);
        _instrumentMock
            .Setup(x => x.GetLastNCandlesAsync(
                CandlestickGranularity.M15, It.IsAny<int>(),
                It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()))
            .ReturnsAsync(candles);

        await InstrumentTools.GetCandles(
            _connectionServiceMock.Object, _cacheReaderMock.Object, "GBP_JPY", "M15", 10);

        _connectionMock.Verify(x => x.GetInstrument(InstrumentName.GBP_JPY), Times.Once);
        _instrumentMock.Verify(x => x.GetLastNCandlesAsync(
            CandlestickGranularity.M15, 10,
            It.IsAny<IEnumerable<GeriRemenyi.Oanda.V20.Sdk.Common.Types.PricingComponent>>()), Times.Once);
    }

    private static List<Candlestick> CreateSampleCandles(int count)
    {
        var candles = new List<Candlestick>();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < count; i++)
        {
            candles.Add(new Candlestick
            {
                Time = baseTime.AddHours(i).ToString("o"),
                Volume = 100 + i,
                Complete = true,
                Mid = new CandlestickData
                {
                    O = 1.1000 + i * 0.001,
                    H = 1.1010 + i * 0.001,
                    L = 1.0990 + i * 0.001,
                    C = 1.1005 + i * 0.001
                }
            });
        }
        return candles;
    }

    private static List<Candlestick> CreateVolatileCandles(int count)
    {
        var candles = new List<Candlestick>();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var random = new Random(42); // Seeded for determinism
        double price = 1.1000;

        for (int i = 0; i < count; i++)
        {
            double change = (random.NextDouble() - 0.5) * 0.02;
            double open = price;
            double close = price + change;
            double high = Math.Max(open, close) + random.NextDouble() * 0.005;
            double low = Math.Min(open, close) - random.NextDouble() * 0.005;
            price = close;

            candles.Add(new Candlestick
            {
                Time = baseTime.AddHours(i).ToString("o"),
                Volume = 100 + random.Next(500),
                Complete = true,
                Mid = new CandlestickData { O = open, H = high, L = low, C = close }
            });
        }
        return candles;
    }

    private static List<Candlestick> CreateTrendingCandles(int count, bool trending)
    {
        var candles = new List<Candlestick>();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double basePrice = 1.1000;
        int waveLength = 8;

        for (int i = 0; i < count; i++)
        {
            int cycle = i / waveLength;
            int pos = i % waveLength;
            double trendOffset = trending ? cycle * 0.0040 : -cycle * 0.0040;

            double mid;
            if (pos < 5)
                mid = basePrice + trendOffset + pos * 0.0010;
            else
                mid = basePrice + trendOffset + 0.0040 - (pos - 4) * 0.0010;

            double open = mid - 0.0001;
            double close = mid + 0.0001;
            double high = mid + 0.0005;
            double low = mid - 0.0005;

            candles.Add(new Candlestick
            {
                Time = baseTime.AddDays(i).ToString("o"),
                Volume = 200,
                Complete = true,
                Mid = new CandlestickData { O = open, H = high, L = low, C = close }
            });
        }
        return candles;
    }
}
