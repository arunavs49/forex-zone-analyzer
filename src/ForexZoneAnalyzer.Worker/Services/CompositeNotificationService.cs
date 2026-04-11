using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// Dispatches zone alerts to multiple notification services in parallel.
/// Individual service failures are logged but do not block other services.
/// </summary>
public class CompositeNotificationService : INotificationService
{
    private readonly IReadOnlyList<INotificationService> _services;
    private readonly ILogger<CompositeNotificationService> _logger;

    public CompositeNotificationService(IEnumerable<INotificationService> services, ILogger<CompositeNotificationService> logger)
    {
        _services = services.ToList();
        _logger = logger;
    }

    public async Task SendZoneAlertAsync(string instrument, string granularity, Zone zone, string trend, CancellationToken cancellationToken)
    {
        var tasks = _services.Select(async service =>
        {
            try
            {
                await service.SendZoneAlertAsync(instrument, granularity, zone, trend, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification service {ServiceType} failed for {Instrument} {Type} zone",
                    service.GetType().Name, instrument, zone.Type);
            }
        });

        await Task.WhenAll(tasks);
    }
}
