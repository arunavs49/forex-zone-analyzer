using ForexZoneAnalyzer.Worker.Configuration;

namespace ForexZoneAnalyzer.Worker.Services;

/// <summary>
/// In-memory config store for development/testing.
/// </summary>
public class InMemoryConfigStore : IConfigStore
{
    private readonly Dictionary<string, PairConfig> _configs = new();
    private readonly Dictionary<string, PairStatus> _statuses = new();
    private readonly object _lock = new();

    private static string Key(string instrument, string granularity) => $"{instrument}_{granularity}";

    public Task<List<PairConfig>> GetEnabledConfigsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_configs.Values.Where(c => c.Enabled).ToList());
        }
    }

    public Task<List<PairConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_configs.Values.ToList());
        }
    }

    public Task<PairConfig?> GetConfigAsync(string instrument, string granularity, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _configs.TryGetValue(Key(instrument, granularity), out var config);
            return Task.FromResult(config);
        }
    }

    public Task UpsertConfigAsync(PairConfig config, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var key = Key(config.Instrument, config.ZoneGranularity);
            if (_configs.TryGetValue(key, out var existing))
                config.ConfigVersion = existing.ConfigVersion + 1;
            else
                config.ConfigVersion = 1;

            config.UpdatedAtUtc = DateTime.UtcNow;
            _configs[key] = config;
        }
        return Task.CompletedTask;
    }

    public Task SetEnabledAsync(string instrument, string granularity, bool enabled, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var key = Key(instrument, granularity);
            if (_configs.TryGetValue(key, out var config))
            {
                config.Enabled = enabled;
                config.ConfigVersion++;
                config.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        return Task.CompletedTask;
    }

    public Task SetEmailEnabledAsync(string instrument, string granularity, bool emailEnabled, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var key = Key(instrument, granularity);
            if (_configs.TryGetValue(key, out var config))
            {
                config.EmailEnabled = emailEnabled;
                config.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        return Task.CompletedTask;
    }

    public Task<PairStatus?> GetStatusAsync(string instrument, string granularity, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _statuses.TryGetValue(Key(instrument, granularity), out var status);
            return Task.FromResult(status);
        }
    }

    public Task<List<PairStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_statuses.Values.ToList());
        }
    }

    public Task UpsertStatusAsync(PairStatus status, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _statuses[Key(status.Instrument, status.ZoneGranularity)] = status;
        }
        return Task.CompletedTask;
    }

    public void InvalidateCache() { /* no-op for in-memory */ }
}
