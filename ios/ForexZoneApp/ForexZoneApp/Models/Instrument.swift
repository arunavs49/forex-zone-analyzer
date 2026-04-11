import Foundation

enum Instrument: String, CaseIterable, Identifiable {
    var id: String { rawValue }

    case EUR_USD
    case GBP_USD
    case USD_JPY
    case USD_CHF
    case AUD_USD
    case NZD_USD
    case USD_CAD
    case EUR_GBP
    case EUR_JPY
    case GBP_JPY
    case EUR_CHF
    case AUD_JPY
    case EUR_AUD
    case GBP_AUD
    case GBP_CHF
    case EUR_CAD
    case AUD_CAD
    case NZD_JPY
    case CAD_JPY
    case CHF_JPY

    var displayName: String {
        rawValue.replacingOccurrences(of: "_", with: "/")
    }

    /// Group instruments by base currency for the list view
    var baseCurrency: String {
        String(rawValue.prefix(3))
    }
}

enum Granularity: String, CaseIterable, Identifiable {
    var id: String { rawValue }

    case M5
    case M15
    case M30
    case H1
    case H4
    case D

    var displayName: String {
        switch self {
        case .M5: return "5 Min"
        case .M15: return "15 Min"
        case .M30: return "30 Min"
        case .H1: return "1 Hour"
        case .H4: return "4 Hour"
        case .D: return "Daily"
        }
    }

    var defaultCandleCount: Int {
        switch self {
        case .M5: return 200
        case .M15: return 200
        case .M30: return 200
        case .H1: return 500
        case .H4: return 500
        case .D: return 500
        }
    }
}
