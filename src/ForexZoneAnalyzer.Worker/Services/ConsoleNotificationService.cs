using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.Worker.Services;

public class ConsoleNotificationService : INotificationService
{
    private readonly ILogger<ConsoleNotificationService> _logger;

    public ConsoleNotificationService(ILogger<ConsoleNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendZoneAlertAsync(string instrument, string granularity, Zone zone, string trend, CancellationToken cancellationToken)
    {
        var message = FormatAlert(instrument, granularity, zone, trend);
        _logger.LogInformation("\n{Alert}", message);
        return Task.CompletedTask;
    }

    internal static string FormatAlert(string instrument, string granularity, Zone zone, string trend)
    {
        return $"""
            ╔══════════════════════════════════════════╗
            ║           NEW ZONE DETECTED              ║
            ╠══════════════════════════════════════════╣
            ║ {instrument,-15} {granularity,-10}            ║
            ║ Type:      {zone.Type,-10} SubZone: {(zone.SubZone ? "Yes" : "No"),-5}  ║
            ║ Freshness: {zone.Freshness,-30}║
            ║ Base:      {zone.BaseRangeLow:F5} – {zone.BaseRangeHigh:F5}      ║
            ║ Candles:   {zone.BaseCandleCount,-30}║
            ║ Trend:     {trend,-30}║
            ╚══════════════════════════════════════════╝
            """;
    }
}
