using System.Collections.Concurrent;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ForexZoneAnalyzer.Worker.Services;

public class InMemoryZoneStore : IZoneStore
{
    private readonly ConcurrentDictionary<string, List<Zone>> _store = new();
    private readonly ConcurrentDictionary<string, string> _trends = new();
    private readonly ConcurrentDictionary<string, DateTime> _updatedAt = new();

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
        _updatedAt[key] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task ClearZonesAsync(string instrument, string granularity, CancellationToken cancellationToken)
    {
        var key = $"{instrument}_{granularity}";
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<string?> GetTrendAsync(string instrument, string granularity, CancellationToken cancellationToken)
    {
        var key = $"{instrument}_{granularity}";
        var trend = _trends.TryGetValue(key, out var existing) ? existing : null;
        return Task.FromResult(trend);
    }

    public Task UpsertTrendAsync(string instrument, string granularity, string trend, CancellationToken cancellationToken)
    {
        var key = $"{instrument}_{granularity}";
        _trends[key] = trend;
        return Task.CompletedTask;
    }

    public Task<DateTime?> GetLastUpdatedAsync(string instrument, string granularity, CancellationToken cancellationToken)
    {
        var key = $"{instrument}_{granularity}";
        DateTime? result = _updatedAt.TryGetValue(key, out var dt) ? dt : null;
        return Task.FromResult(result);
    }
}
