using System.Collections.Concurrent;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.Worker.Services;

public class InMemoryZoneStore : IZoneStore
{
    private readonly ConcurrentDictionary<string, List<Zone>> _store = new();

    public Task<List<Zone>> GetZonesAsync(string instrument, string granularity, CancellationToken cancellationToken)
    {
        var key = $"{instrument}_{granularity}";
        var zones = _store.TryGetValue(key, out var existing) ? new List<Zone>(existing) : new List<Zone>();
        return Task.FromResult(zones);
    }

    public Task UpsertZonesAsync(string instrument, string granularity, List<Zone> zones, CancellationToken cancellationToken)
    {
        var key = $"{instrument}_{granularity}";
        _store[key] = new List<Zone>(zones);
        return Task.CompletedTask;
    }
}
