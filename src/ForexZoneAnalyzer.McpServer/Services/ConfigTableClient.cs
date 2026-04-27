using Azure.Data.Tables;

namespace ForexZoneAnalyzer.McpServer.Services;

/// <summary>
/// Typed wrapper for the pairconfigs TableClient, used for DI injection into MCP tools.
/// </summary>
public class ConfigTableClient
{
    public TableClient Configs { get; }
    public TableClient Statuses { get; }

    public ConfigTableClient(TableClient configs, TableClient statuses)
    {
        Configs = configs;
        Statuses = statuses;
    }
}
