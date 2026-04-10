using GeriRemenyi.Oanda.V20.Sdk;

namespace ForexZoneAnalyzer.McpServer.Services;

public interface IOandaConnectionService
{
    Task<IOandaApiConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
