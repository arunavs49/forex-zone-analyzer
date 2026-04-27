namespace ForexZoneAnalyzer.Worker.Configuration;

public class CleanupSettings
{
    public int IntervalHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 30;
}
