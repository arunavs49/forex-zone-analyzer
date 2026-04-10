using ForexZoneAnalyzer.Worker.Services;
using GeriRemenyi.Oanda.V20.Client.Model;
using Xunit;

namespace ForexZoneAnalyzer.Worker.Test;

/// <summary>
/// Tests the candle-aligned scheduling logic. The worker runs 1 minute after
/// each candle close, with the interval derived from the zone granularity.
/// </summary>
public class CandleAlignedScheduleTests
{
    // --- M15 tests (runs at :01, :16, :31, :46) ---

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
    public void M15_GetDelayUntilNextSlot_ReturnsCorrectMinutes(string timeStr, double expectedMinutes)
    {
        var utcNow = DateTime.Parse($"2026-04-10T{timeStr}Z").ToUniversalTime();
        var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow, 15);

        Assert.InRange(delay.TotalMinutes, expectedMinutes - 0.5, expectedMinutes + 0.5);
    }

    [Fact]
    public void M15_TargetIsAlwaysCandleAligned()
    {
        var validMinutes = new[] { 1, 16, 31, 46 };

        for (int m = 0; m < 60; m++)
        {
            for (int s = 0; s < 60; s += 15)
            {
                var utcNow = new DateTime(2026, 4, 10, 12, m, s, DateTimeKind.Utc);
                var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow, 15);
                var target = utcNow + delay;

                Assert.Contains(target.Minute, validMinutes);
                Assert.Equal(0, target.Second);
            }
        }
    }

    // --- H1 tests (runs at :01 each hour) ---

    [Theory]
    [InlineData("12:00:00", 1)]    // exactly at candle close → 1 min to :01
    [InlineData("12:01:00", 60)]   // exactly at run slot → next is 13:01
    [InlineData("12:30:00", 31)]   // mid-hour → 31 min to 13:01
    [InlineData("12:59:00", 2)]    // 2 min before 13:01
    [InlineData("23:59:00", 2)]    // 2 min to 00:01 next day
    public void H1_GetDelayUntilNextSlot_ReturnsCorrectMinutes(string timeStr, double expectedMinutes)
    {
        var utcNow = DateTime.Parse($"2026-04-10T{timeStr}Z").ToUniversalTime();
        var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow, 60);

        Assert.InRange(delay.TotalMinutes, expectedMinutes - 0.5, expectedMinutes + 0.5);
    }

    [Fact]
    public void H1_TargetIsAlwaysHourAligned()
    {
        for (int h = 0; h < 24; h++)
        {
            for (int m = 0; m < 60; m += 5)
            {
                var utcNow = new DateTime(2026, 4, 10, h, m, 0, DateTimeKind.Utc);
                var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow, 60);
                var target = utcNow + delay;

                Assert.Equal(1, target.Minute);
                Assert.Equal(0, target.Second);
            }
        }
    }

    // --- H4 tests (runs at :01 past every 4th hour: 00:01, 04:01, 08:01, ...) ---

    [Theory]
    [InlineData("00:00:00", 1)]    // midnight → 00:01
    [InlineData("00:01:00", 240)]  // just ran → next is 04:01 (240 min)
    [InlineData("03:59:00", 2)]    // 2 min before 04:01
    [InlineData("04:00:00", 1)]    // candle close → 04:01
    [InlineData("06:00:00", 121)]  // mid-slot → 08:01 (121 min)
    public void H4_GetDelayUntilNextSlot_ReturnsCorrectMinutes(string timeStr, double expectedMinutes)
    {
        var utcNow = DateTime.Parse($"2026-04-10T{timeStr}Z").ToUniversalTime();
        var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow, 240);

        Assert.InRange(delay.TotalMinutes, expectedMinutes - 0.5, expectedMinutes + 0.5);
    }

    // --- Granularity mapping tests ---

    [Theory]
    [InlineData(CandlestickGranularity.M1, 1)]
    [InlineData(CandlestickGranularity.M5, 5)]
    [InlineData(CandlestickGranularity.M15, 15)]
    [InlineData(CandlestickGranularity.M30, 30)]
    [InlineData(CandlestickGranularity.H1, 60)]
    [InlineData(CandlestickGranularity.H4, 240)]
    [InlineData(CandlestickGranularity.D, 1440)]
    public void GetGranularityMinutes_ReturnsCorrectValue(CandlestickGranularity granularity, int expectedMinutes)
    {
        Assert.Equal(expectedMinutes, ZoneMonitorService.GetGranularityMinutes(granularity));
    }

    // --- General invariants ---

    [Theory]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(240)]
    public void DelayIsAlwaysPositive(int intervalMinutes)
    {
        for (int m = 0; m < 60; m++)
        {
            var utcNow = new DateTime(2026, 4, 10, 12, m, 0, DateTimeKind.Utc);
            var delay = ZoneMonitorService.GetDelayUntilNextSlot(utcNow, intervalMinutes);
            Assert.True(delay > TimeSpan.Zero, $"Delay should be positive at minute {m} for {intervalMinutes}min interval, got {delay}");
        }
    }
}
