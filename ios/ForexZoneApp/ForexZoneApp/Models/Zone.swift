import Foundation

enum ZoneType: String, Codable {
    case Supply
    case Demand
}

enum ZoneFreshness: String, Codable {
    case Untested
    case Tested
    case Broken
}

struct Zone: Identifiable, Codable {
    var id: String { "\(type.rawValue)_\(startTime)_\(baseRangeHigh)_\(baseRangeLow)" }

    let type: ZoneType
    let freshness: ZoneFreshness
    let worked: Bool?
    let subZone: Bool
    let baseRangeHigh: Double
    let baseRangeLow: Double
    let baseCandleCount: Int
    let startTime: String
    let endTime: String

    enum CodingKeys: String, CodingKey {
        case type = "Type"
        case freshness = "Freshness"
        case worked = "Worked"
        case subZone = "SubZone"
        case baseRangeHigh = "BaseRangeHigh"
        case baseRangeLow = "BaseRangeLow"
        case baseCandleCount = "BaseCandleCount"
        case startTime = "StartTime"
        case endTime = "EndTime"
    }

    var startDate: Date? {
        parseISO8601(startTime)
    }

    var endDate: Date? {
        parseISO8601(endTime)
    }
}
