using System.ComponentModel;
using Azure.Data.Tables;
using GeriRemenyi.Oanda.V20.Client.Model;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace ForexZoneAnalyzer.McpServer.Tools;

[McpServerToolType]
public sealed class StoredZoneTools
{
    [McpServerTool(Name = "get_stored_zones"), Description("Get pre-computed supply/demand zones and trend from storage for a forex instrument and timeframe. Zones are refreshed by the background worker. Supported granularities: M5, M15, M30, H1, H4, D.")]
    public static async Task<string> GetStoredZones(
        TableClient tableClient,
        [Description("Instrument name (e.g. 'EUR_USD', 'GBP_USD', 'USD_CAD')")] string instrument,
        [Description("Zone granularity: M5, M15, M30, H1, H4, D")] string granularity,
        CancellationToken cancellationToken = default)
    {
        var partitionKey = $"{instrument}_{granularity}";

        var zones = new List<Zone>();
        string? trend = null;

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{partitionKey}'",
            cancellationToken: cancellationToken))
        {
            if (entity.RowKey == "_trend_")
            {
                trend = entity.GetString("Trend");
                continue;
            }

            var zone = DeserializeZone(entity);
            if (zone != null)
                zones.Add(zone);
        }

        var supplyZones = zones.Where(z => z.Type == ZoneType.Supply).ToList();
        var demandZones = zones.Where(z => z.Type == ZoneType.Demand).ToList();

        var result = new
        {
            Instrument = instrument,
            Granularity = granularity,
            Trend = trend ?? "Unknown",
            TotalZones = zones.Count,
            SupplyZones = supplyZones.Select(FormatZone).ToList(),
            DemandZones = demandZones.Select(FormatZone).ToList()
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    private static Zone? DeserializeZone(TableEntity entity)
    {
        try
        {
            return new Zone
            {
                Type = Enum.Parse<ZoneType>(entity.GetString("Type")),
                StartTime = entity.GetDateTimeOffset("StartTime")?.LocalDateTime ?? DateTime.MinValue,
                EndTime = entity.GetDateTimeOffset("EndTime")?.LocalDateTime ?? DateTime.MinValue,
                LegInStartPrice = entity.GetDouble("LegInStartPrice") ?? 0,
                LegInEndPrice = entity.GetDouble("LegInEndPrice") ?? 0,
                LegOutStartPrice = entity.GetDouble("LegOutStartPrice") ?? 0,
                LegOutEndPrice = entity.GetDouble("LegOutEndPrice") ?? 0,
                BaseRangeHigh = entity.GetDouble("BaseRangeHigh") ?? 0,
                BaseRangeLow = entity.GetDouble("BaseRangeLow") ?? 0,
                BaseCandleCount = entity.GetInt32("BaseCandleCount") ?? 0,
                Freshness = Enum.Parse<ZoneFreshness>(entity.GetString("Freshness") ?? "Untested"),
                Worked = entity.GetBoolean("Worked"),
                SubZone = entity.GetBoolean("SubZone") ?? false
            };
        }
        catch
        {
            return null;
        }
    }

    private static object FormatZone(Zone z) => new
    {
        Type = z.Type.ToString(),
        Freshness = z.Freshness.ToString(),
        Worked = z.Worked,
        SubZone = z.SubZone,
        BaseRangeHigh = z.BaseRangeHigh,
        BaseRangeLow = z.BaseRangeLow,
        BaseCandleCount = z.BaseCandleCount,
        StartTime = z.StartTime.ToString("o"),
        EndTime = z.EndTime.ToString("o")
    };
}
