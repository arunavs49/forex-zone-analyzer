using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.Worker.Services;

public interface INotificationService
{
    Task SendZoneAlertAsync(string instrument, string granularity, Zone zone, string trend, CancellationToken cancellationToken);
}
