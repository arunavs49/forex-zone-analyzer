using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.Worker.Services;

public interface IZoneStore
{
    Task<List<Zone>> GetZonesAsync(string instrument, string granularity, CancellationToken cancellationToken);
    Task UpsertZonesAsync(string instrument, string granularity, List<Zone> zones, CancellationToken cancellationToken);
    Task<string?> GetTrendAsync(string instrument, string granularity, CancellationToken cancellationToken);
    Task UpsertTrendAsync(string instrument, string granularity, string trend, CancellationToken cancellationToken);
}
