import Foundation
import SwiftUI

@MainActor
class ChartViewModel: ObservableObject {
    @Published var candles: [Candle] = []
    @Published var supplyZones: [Zone] = []
    @Published var demandZones: [Zone] = []
    @Published var trend: String = ""
    @Published var isLoading = false
    @Published var error: String?
    @Published var pairStatus: PairStatusResponse?

    let instrument: Instrument
    @Published var granularity: Granularity

    private let service = ForexDataService()

    init(instrument: Instrument, granularity: Granularity = .H1) {
        self.instrument = instrument
        self.granularity = granularity
    }

    /// Zone the user tapped in the list — chart scrolls to its formation
    @Published var focusedZone: Zone?

    var allZones: [Zone] {
        supplyZones + demandZones
    }

    var activeZones: [Zone] {
        allZones.filter { $0.freshness != .Broken }
    }

    // MARK: - Chart zones (only zones whose formation overlaps loaded candles)

    private var candleTimeRange: (start: Date, end: Date)? {
        guard let first = candles.first?.date,
              let last = candles.last?.date else { return nil }
        return (first, last)
    }

    private func zonesInCandleRange(_ zones: [Zone]) -> [Zone] {
        guard let range = candleTimeRange else { return [] }
        return zones.filter { zone in
            guard let zoneStart = zone.startDate else { return false }
            return zoneStart >= range.start && zoneStart <= range.end
        }
    }

    var chartSupplyZones: [Zone] {
        zonesInCandleRange(supplyZones)
    }

    var chartDemandZones: [Zone] {
        zonesInCandleRange(demandZones)
    }

    // MARK: - List zones (untested/tested within 1000 pips + newest 10)

    private var currentPrice: Double? {
        candles.last?.close
    }

    /// List-visible zones: untested/tested within 1000 pips of price + latest 10 (any freshness)
    var visibleZones: [Zone] {
        let isJpy = instrument.rawValue.contains("JPY")
        let pipMultiplier: Double = isJpy ? 100 : 10000
        let maxPipDistance: Double = 1000

        // Untested/Tested within 1000 pips of current price
        let nearbyActive: [Zone]
        if let price = currentPrice {
            nearbyActive = allZones.filter { zone in
                guard zone.freshness != .Broken else { return false }
                let midPrice = (zone.baseRangeHigh + zone.baseRangeLow) / 2
                let pipsAway = abs(midPrice - price) * pipMultiplier
                return pipsAway <= maxPipDistance
            }
        } else {
            nearbyActive = allZones.filter { $0.freshness != .Broken }
        }

        // Latest 10 zones by start time (any freshness)
        let sortedByTime = allZones.sorted { ($0.startDate ?? .distantPast) > ($1.startDate ?? .distantPast) }
        let recent10 = Array(sortedByTime.prefix(10))

        // Union by id
        var seen = Set<String>()
        var result: [Zone] = []
        for zone in nearbyActive + recent10 {
            if seen.insert(zone.id).inserted {
                result.append(zone)
            }
        }
        return result
    }

    var visibleSupplyZones: [Zone] {
        visibleZones.filter { $0.type == .Supply }
    }

    var visibleDemandZones: [Zone] {
        visibleZones.filter { $0.type == .Demand }
    }

    func loadData(settings: AppSettings, authService: AuthService? = nil) async {
        isLoading = true
        error = nil

        do {
            if let auth = authService, auth.isSignedIn {
                try await service.configure(url: settings.mcpServerURL, tokenProvider: { await auth.getAccessToken() })
            } else {
                try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }

            // Candles fetched live; zones + trend from storage in one call
            async let candlesFetch = service.getCandles(
                instrument: instrument.rawValue,
                granularity: granularity.rawValue,
                count: granularity.defaultCandleCount
            )
            async let storedFetch = service.getStoredZones(
                instrument: instrument.rawValue,
                granularity: granularity.rawValue
            )

            let (fetchedCandles, storedResponse) = try await (candlesFetch, storedFetch)

            candles = fetchedCandles
            supplyZones = storedResponse.supplyZones
            demandZones = storedResponse.demandZones
            trend = storedResponse.trend ?? "Unknown"

            // Fetch pair status (non-blocking — don't fail if unavailable)
            do {
                pairStatus = try await service.getPairStatus(
                    instrument: instrument.rawValue,
                    granularity: granularity.rawValue
                )
            } catch {
                pairStatus = nil
            }
        } catch {
            self.error = error.localizedDescription
        }

        isLoading = false
    }
}
