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

    var allZones: [Zone] {
        supplyZones + demandZones
    }

    var activeZones: [Zone] {
        allZones.filter { $0.freshness != .Broken }
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
