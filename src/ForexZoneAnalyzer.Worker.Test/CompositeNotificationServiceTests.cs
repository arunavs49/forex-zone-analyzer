using ForexZoneAnalyzer.Worker.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

public class CompositeNotificationServiceTests
{
    private static Zone CreateTestZone() => new()
    {
        Type = ZoneType.Demand,
        Freshness = ZoneFreshness.Untested,
        SubZone = false,
        BaseRangeLow = 1.08500,
        BaseRangeHigh = 1.08700,
        BaseCandleCount = 3,
        StartTime = DateTime.UtcNow,
        EndTime = DateTime.UtcNow
    };

    [Fact]
    public async Task SendZoneAlertAsync_CallsAllServices()
    {
        var service1 = new FakeNotificationService();
        var service2 = new FakeNotificationService();
        var logger = new LoggerFactory().CreateLogger<CompositeNotificationService>();
        var composite = new CompositeNotificationService([service1, service2], logger);

        await composite.SendZoneAlertAsync("EUR_USD", "M15", CreateTestZone(), "Bullish", CancellationToken.None);

        Assert.Equal(1, service1.CallCount);
        Assert.Equal(1, service2.CallCount);
    }

    [Fact]
    public async Task SendZoneAlertAsync_ContinuesWhenOneServiceFails()
    {
        var failingService = new FakeNotificationService { ShouldThrow = true };
        var successService = new FakeNotificationService();
        var logger = new LoggerFactory().CreateLogger<CompositeNotificationService>();
        var composite = new CompositeNotificationService([failingService, successService], logger);

        // Should not throw
        await composite.SendZoneAlertAsync("EUR_USD", "M15", CreateTestZone(), "Bullish", CancellationToken.None);

        Assert.Equal(1, failingService.CallCount);
        Assert.Equal(1, successService.CallCount);
    }

    [Fact]
    public async Task SendZoneAlertAsync_WithEmptyServices_DoesNotThrow()
    {
        var logger = new LoggerFactory().CreateLogger<CompositeNotificationService>();
        var composite = new CompositeNotificationService([], logger);

        await composite.SendZoneAlertAsync("EUR_USD", "M15", CreateTestZone(), "Bullish", CancellationToken.None);
    }

    private class FakeNotificationService : INotificationService
    {
        public int CallCount { get; private set; }
        public bool ShouldThrow { get; init; }

        public Task SendZoneAlertAsync(string instrument, string granularity, Zone zone, string trend, CancellationToken cancellationToken)
        {
            CallCount++;
            if (ShouldThrow)
                throw new InvalidOperationException("Simulated notification failure");
            return Task.CompletedTask;
        }
    }
}
