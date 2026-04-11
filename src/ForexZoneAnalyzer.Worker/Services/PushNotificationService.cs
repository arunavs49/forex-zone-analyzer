using System.Text.Json;
using GeriRemenyi.Oanda.V20.Client.Model;
using Microsoft.Azure.NotificationHubs;

namespace ForexZoneAnalyzer.Worker.Services;

public class PushNotificationService : INotificationService
{
    private readonly INotificationHubClient _hubClient;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(INotificationHubClient hubClient, ILogger<PushNotificationService> logger)
    {
        _hubClient = hubClient;
        _logger = logger;
    }

    public async Task SendZoneAlertAsync(string instrument, string granularity, Zone zone, string trend, CancellationToken cancellationToken)
    {
        var payload = BuildApnsPayload(instrument, granularity, zone, trend);

        try
        {
            await SendWithRetryAsync(payload, cancellationToken);
            _logger.LogInformation("Push notification sent for {Instrument} {Type} zone", instrument, zone.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification for {Instrument} {Type} zone after retries", instrument, zone.Type);
        }
    }

    internal static string BuildApnsPayload(string instrument, string granularity, Zone zone, string trend)
    {
        var payload = new
        {
            aps = new
            {
                alert = new
                {
                    title = $"New {zone.Type} Zone",
                    subtitle = $"{instrument} {granularity}",
                    body = $"{zone.Freshness} zone at {zone.BaseRangeLow:F5} – {zone.BaseRangeHigh:F5} (Trend: {trend})"
                },
                sound = "default",
                category = "ZONE_ALERT"
            },
            zone = new
            {
                instrument,
                granularity,
                type = zone.Type.ToString(),
                freshness = zone.Freshness.ToString(),
                subZone = zone.SubZone,
                baseRangeLow = zone.BaseRangeLow,
                baseRangeHigh = zone.BaseRangeHigh,
                baseCandleCount = zone.BaseCandleCount,
                trend
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task SendWithRetryAsync(string payload, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var notification = new AppleNotification(payload);
                await _hubClient.SendNotificationAsync(notification, "all", cancellationToken);
                return;
            }
            catch (Exception) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                _logger.LogWarning("Push send attempt {Attempt} failed, retrying in {Delay}s", attempt + 1, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
