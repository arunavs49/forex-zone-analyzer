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

    /// Compute limit order parameters for this zone.
    ///
    /// - Supply (Short): entry at baseRangeLow, SL above baseRangeHigh + 5 pips, TP = 2× distance
    /// - Demand (Long):  entry at baseRangeHigh, SL below baseRangeLow − 5 pips, TP = 2× distance
    /// - Units sized so that the SL distance consumes exactly `riskAmountUSD`.
    func orderParameters(instrumentSymbol: String, riskAmountUSD: Double) -> ZoneOrderParameters {
        let pipSize: Double = instrumentSymbol.contains("JPY") ? 0.01 : 0.0001
        let slBuffer = 5

        let baseWidthPips = Int(((baseRangeHigh - baseRangeLow) / pipSize).rounded())
        let slPips = baseWidthPips + slBuffer
        let tpPips = slPips * 2

        switch type {
        case .Supply:
            let entryPrice = baseRangeLow
            let slPrice = baseRangeHigh + Double(slBuffer) * pipSize
            let tpPrice = entryPrice - Double(tpPips) * pipSize
            let units = max(1000, Int((riskAmountUSD / (Double(slPips) * pipSize)).rounded(.toNearestOrEven) / 1000) * 1000)
            return ZoneOrderParameters(
                direction: "Short",
                entryPrice: entryPrice,
                stopLossPrice: slPrice,
                takeProfitPrice: tpPrice,
                stopLossPips: slPips,
                takeProfitPips: tpPips,
                units: units,
                riskAmountUSD: riskAmountUSD
            )
        case .Demand:
            let entryPrice = baseRangeHigh
            let slPrice = baseRangeLow - Double(slBuffer) * pipSize
            let tpPrice = entryPrice + Double(tpPips) * pipSize
            let units = max(1000, Int((riskAmountUSD / (Double(slPips) * pipSize)).rounded(.toNearestOrEven) / 1000) * 1000)
            return ZoneOrderParameters(
                direction: "Long",
                entryPrice: entryPrice,
                stopLossPrice: slPrice,
                takeProfitPrice: tpPrice,
                stopLossPips: slPips,
                takeProfitPips: tpPips,
                units: units,
                riskAmountUSD: riskAmountUSD
            )
        }
    }
}

/// Computed order parameters derived from a Zone.
struct ZoneOrderParameters {
    let direction: String
    let entryPrice: Double
    let stopLossPrice: Double
    let takeProfitPrice: Double
    let stopLossPips: Int
    let takeProfitPips: Int
    let units: Int
    let riskAmountUSD: Double
}
