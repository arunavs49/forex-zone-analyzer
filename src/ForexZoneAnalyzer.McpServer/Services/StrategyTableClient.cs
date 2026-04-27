using Azure.Data.Tables;
using Azure.Storage.Queues;

namespace ForexZoneAnalyzer.McpServer.Services;

/// <summary>
/// Typed wrapper for strategy-related Azure clients, used for DI injection into MCP tools.
/// </summary>
public class StrategyTableClient
{
    public TableClient Runs { get; }
    public QueueClient Queue { get; }

    public StrategyTableClient(TableClient runs, QueueClient queue)
    {
        Runs = runs;
        Queue = queue;
    }
}
