import Foundation

struct ZoneAnalysisResponse: Codable {
    let instrument: String
    let granularity: String
    let trend: String?
    let candlesAnalyzed: Int?
    let totalZones: Int
    let supplyZones: [Zone]
    let demandZones: [Zone]

    enum CodingKeys: String, CodingKey {
        case instrument = "Instrument"
        case granularity = "Granularity"
        case trend = "Trend"
        case candlesAnalyzed = "CandlesAnalyzed"
        case totalZones = "TotalZones"
        case supplyZones = "SupplyZones"
        case demandZones = "DemandZones"
    }
}
