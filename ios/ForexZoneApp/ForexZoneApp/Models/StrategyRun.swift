import Foundation

/// Represents a strategy optimization run
struct StrategyRun: Identifiable, Codable {
    var id: String { RunId }

    let RunId: String
    let Status: String
    let RequestedUtc: String?
    let CompletedUtc: String?
    let LookbackMonths: Int?
    let BestScore: Double?
    let BestWinRate: Double?
    let BestTradedZones: Int?
    let BestAvgRR: Double?
    let TotalCombos: Int?
    let ScoredCombos: Int?
    let Error: String?
    let BestZoneConfig: StrategyZoneConfig?
    let BestTrendConfig: StrategyTrendConfig?
    let TopResults: [StrategyTopResult]?
}

struct StrategyZoneConfig: Codable {
    let MinBaseLength: Int?
    let MaxBaseLength: Int?
    let MinLegInToBaseRangeRatio: Double?
    let MinLegOutToBaseRangeRatio: Double?
}

struct StrategyTrendConfig: Codable {
    let SwingLookback: Int?
    let TrendCandleCount: Int?
    let MinSwingPoints: Int?
}

struct StrategyTopResult: Identifiable, Codable {
    var id: String { "\(Score)-\(WinRate)-\(TradedZones)" }

    let Score: Double
    let WinRate: Double
    let TradedZones: Int
    let AverageRR: Double
    let Wins: Int
    let Losses: Int
    let Timeouts: Int
    let Zone: StrategyZoneConfig?
    let Trend: StrategyTrendConfig?
}

struct StrategyRunListResponse: Codable {
    let Instrument: String?
    let Granularity: String?
    let TotalRuns: Int
    let Runs: [StrategyRunSummary]
}

struct StrategyRunSummary: Identifiable, Codable {
    var id: String { RunId ?? UUID().uuidString }

    let RunId: String?
    let Instrument: String?
    let Granularity: String?
    let Status: String?
    let RequestedUtc: String?
    let CompletedUtc: String?
    let LookbackMonths: Int?
    let BestScore: Double?
    let BestWinRate: Double?
    let Error: String?
}

struct RecentStrategyRunListResponse: Codable {
    let TotalRuns: Int
    let Runs: [StrategyRunSummary]
}

struct StartRunResponse: Codable {
    let Status: String?
    let RunId: String?
    let Error: String?
    let Message: String?
}
