import Foundation

/// Per-pair-per-timeframe configuration from the MCP server
struct PairConfig: Identifiable, Codable, Hashable {
    var id: String { "\(Instrument)_\(ZoneGranularity)" }

    let Instrument: String
    let ZoneGranularity: String
    let TrendGranularity: String
    let Enabled: Bool
    let EmailEnabled: Bool
    let MinBaseLength: Int?
    let MaxBaseLength: Int?
    let MinLegInToBaseRangeRatio: Double?
    let MinLegOutToBaseRangeRatio: Double?
    let SwingLookback: Int?
    let TrendCandleCount: Int?
    let MinSwingPoints: Int?
    let ConfigVersion: Int
    let UpdatedAtUtc: String?

    // Status fields (from joined response)
    let LastProcessedUtc: String?
    let ZoneCount: Int?
    let Trend: String?

    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }

    static func == (lhs: PairConfig, rhs: PairConfig) -> Bool {
        lhs.id == rhs.id
    }
}

struct PairConfigListResponse: Codable {
    let TotalConfigs: Int
    let Configs: [PairConfig]
}

struct PairConfigUpdateResponse: Codable {
    let Status: String?
    let Instrument: String?
    let Granularity: String?
    let ConfigVersion: Int?
    let Error: String?
}

/// Status-only response
struct PairStatusResponse: Codable {
    let Instrument: String?
    let Granularity: String?
    let LastProcessedUtc: String?
    let ConfigVersionProcessed: Int?
    let ZoneCount: Int?
    let Trend: String?
    let Error: String?
}
