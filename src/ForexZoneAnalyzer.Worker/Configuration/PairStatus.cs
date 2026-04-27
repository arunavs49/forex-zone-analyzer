namespace ForexZoneAnalyzer.Worker.Configuration;

/// <summary>
/// Runtime status for a pair+TF, written by Worker only.
/// Stored in Azure Table Storage (pairstatus table).
/// PartitionKey = instrument, RowKey = zone granularity.
/// </summary>
public class PairStatus
{
    public string Instrument { get; set; } = "";
    public string ZoneGranularity { get; set; } = "";
    public DateTime? LastProcessedUtc { get; set; }
    public int ConfigVersionProcessed { get; set; }
    public int ZoneCount { get; set; }
    public string Trend { get; set; } = "Unknown";
}
