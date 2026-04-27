using ForexZoneAnalyzer.Worker.Configuration;

namespace ForexZoneAnalyzer.Worker.Services;

public interface IConfigStore
{
    /// <summary>
    /// Get all configs that have Enabled=true.
    /// </summary>
    Task<List<PairConfig>> GetEnabledConfigsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all configs (enabled and disabled).
    /// </summary>
    Task<List<PairConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get config for a specific pair+TF. Returns null if not found.
    /// </summary>
    Task<PairConfig?> GetConfigAsync(string instrument, string granularity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create or update a pair config. Increments ConfigVersion automatically.
    /// </summary>
    Task UpsertConfigAsync(PairConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggle processing for a pair+TF.
    /// </summary>
    Task SetEnabledAsync(string instrument, string granularity, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggle email alerts for a pair+TF.
    /// </summary>
    Task SetEmailEnabledAsync(string instrument, string granularity, bool emailEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get status for a specific pair+TF. Returns null if not found.
    /// </summary>
    Task<PairStatus?> GetStatusAsync(string instrument, string granularity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get statuses for all pair+TFs that have been processed.
    /// </summary>
    Task<List<PairStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update status after processing a pair+TF (Worker only).
    /// </summary>
    Task UpsertStatusAsync(PairStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force refresh of the cached config data.
    /// </summary>
    void InvalidateCache();
}
