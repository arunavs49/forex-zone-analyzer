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

    /// Visible zones: all untested/tested + up to 10 most recent (any state)
    var visibleZones: [Zone] {
        let untestedTested = allZones.filter { $0.freshness != .Broken }
        let sortedByTime = allZones.sorted { ($0.startDate ?? .distantPast) > ($1.startDate ?? .distantPast) }
        let recent10 = Array(sortedByTime.prefix(10))
        // Union by id
        var seen = Set<String>()
        var result: [Zone] = []
        for zone in untestedTested + recent10 {
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

    func loadData(settings: AppSettings) async {
        isLoading = true
        error = nil

        do {
            try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)

            async let candlesFetch = service.getCandles(
                instrument: instrument.rawValue,
                granularity: granularity.rawValue,
                count: granularity.defaultCandleCount
            )
            async let zonesFetch = service.getZones(
                instrument: instrument.rawValue,
                granularity: granularity.rawValue,
                count: granularity.defaultCandleCount
            )
            async let trendFetch = service.getTrend(
                instrument: instrument.rawValue,
                granularity: granularity.rawValue
            )

            let (fetchedCandles, zoneResponse, fetchedTrend) = try await (candlesFetch, zonesFetch, trendFetch)

            candles = fetchedCandles
            supplyZones = zoneResponse.supplyZones
            demandZones = zoneResponse.demandZones
            trend = fetchedTrend
        } catch {
            self.error = error.localizedDescription
        }

        isLoading = false
    }
}
