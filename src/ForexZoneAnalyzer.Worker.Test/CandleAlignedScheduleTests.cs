using ForexZoneAnalyzer.Worker.Services;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

/// <summary>
/// Tests the candle-aligned scheduling logic. The worker should run at
/// :01, :16, :31, :46 UTC (1 minute after each M15 candle close).
/// </summary>
public class CandleAlignedScheduleTests
{
    [Theory]
    [InlineData("12:00:00", 1)]    // exactly at candle close → 1 min to :01
    [InlineData("12:00:30", 0.5)]  // 30s past close → 30s to :01
    [InlineData("12:01:00", 15)]   // exactly at :01 run slot → next is :16 (15 min)
    [InlineData("12:02:00", 14)]   // past :01 → 14 min to :16
    [InlineData("12:14:00", 2)]    // 2 min before :16
    [InlineData("12:15:00", 1)]    // candle close → 1 min to :16
    [InlineData("12:16:00", 15)]   // exactly at :16 run slot → next is :31 (15 min)
    [InlineData("12:16:30", 14.5)] // 30s past :16 → 14.5 min to :31
    [InlineData("12:30:00", 1)]    // candle close → 1 min to :31
    [InlineData("12:31:00", 15)]   // exactly at :31 run slot → next is :46 (15 min)
    [InlineData("12:45:00", 1)]    // candle close → 1 min to :46
    [InlineData("12:46:00", 15)]   // exactly at :46 → next is :01 (15 min)
    [InlineData("23:59:00", 2)]    // 2 min to 00:01 next day
    public void GetDelayUntilNextSlot_ReturnsCorrectMinutes(string timeStr, double expectedMinutes)
    {
        var utcNow = DateTime.Parse($"2026-04-10T{timeStr}Z").ToUniversalTime();
        var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow);

        Assert.InRange(delay.TotalMinutes, expectedMinutes - 0.5, expectedMinutes + 0.5);
    }

    [Fact]
    public void GetDelayUntilNextSlot_AlwaysPositive()
    {
        // Test across every minute of an hour
        for (int m = 0; m < 60; m++)
        {
            var utcNow = new DateTime(2026, 4, 10, 12, m, 0, DateTimeKind.Utc);
            var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow);
            Assert.True(delay > TimeSpan.Zero, $"Delay should be positive at minute {m}, got {delay}");
        }
    }

    [Fact]
    public void GetDelayUntilNextSlot_TargetIsAlwaysCandleAligned()
    {
        // The target time (now + delay) should always land on :01, :16, :31, or :46
        var validMinutes = new[] { 1, 16, 31, 46 };

        for (int m = 0; m < 60; m++)
        {
            for (int s = 0; s < 60; s += 15)
            {
                var utcNow = new DateTime(2026, 4, 10, 12, m, s, DateTimeKind.Utc);
                var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow);
                var target = utcNow + delay;

                Assert.Contains(target.Minute, validMinutes);
                Assert.Equal(0, target.Second);
            }
        }
    }
}
