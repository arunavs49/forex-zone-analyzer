import Foundation

private let _isoFormatterFrac: ISO8601DateFormatter = {
    let f = ISO8601DateFormatter()
    f.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
    return f
}()

private let _isoFormatter: ISO8601DateFormatter = {
    let f = ISO8601DateFormatter()
    f.formatOptions = [.withInternetDateTime]
    return f
}()

func parseISO8601(_ str: String) -> Date? {
    _isoFormatterFrac.date(from: str) ?? _isoFormatter.date(from: str)
}

struct Candle: Identifiable, Codable {
    var id: String { time }

    let time: String
    let open: Double?
    let high: Double?
    let low: Double?
    let close: Double?
    let volume: Int?
    let complete: Bool?

    enum CodingKeys: String, CodingKey {
        case time = "Time"
        case open = "Open"
        case high = "High"
        case low = "Low"
        case close = "Close"
        case volume = "Volume"
        case complete = "Complete"
    }

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        time = try c.decode(String.self, forKey: .time)
        open = try c.decodeIfPresent(Double.self, forKey: .open)
        high = try c.decodeIfPresent(Double.self, forKey: .high)
        low = try c.decodeIfPresent(Double.self, forKey: .low)
        close = try c.decodeIfPresent(Double.self, forKey: .close)
        // Volume may arrive as Double from some JSON serializers
        if let intVal = try? c.decodeIfPresent(Int.self, forKey: .volume) {
            volume = intVal
        } else if let dblVal = try? c.decodeIfPresent(Double.self, forKey: .volume) {
            volume = Int(dblVal)
        } else {
            volume = nil
        }
        complete = try c.decodeIfPresent(Bool.self, forKey: .complete)
    }

    var date: Date? {
        parseISO8601(time)
    }

    var isBullish: Bool {
        guard let o = open, let c = close else { return true }
        return c >= o
    }

    var bodyTop: Double {
        guard let o = open, let c = close else { return high ?? 0 }
        return max(o, c)
    }

    var bodyBottom: Double {
        guard let o = open, let c = close else { return low ?? 0 }
        return min(o, c)
    }
}
