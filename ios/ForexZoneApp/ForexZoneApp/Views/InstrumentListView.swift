import SwiftUI

struct InstrumentListView: View {
    @EnvironmentObject var settings: AppSettings

    private let majorPairs: [Instrument] = [.EUR_USD, .GBP_USD, .USD_JPY, .USD_CHF, .AUD_USD, .NZD_USD, .USD_CAD]
    private let crossPairs: [Instrument] = [.EUR_GBP, .EUR_JPY, .GBP_JPY, .EUR_CHF, .AUD_JPY, .EUR_AUD, .GBP_AUD, .GBP_CHF, .EUR_CAD, .AUD_CAD, .NZD_JPY, .CAD_JPY, .CHF_JPY]

    var body: some View {
        List {
            if !settings.isConfigured {
                Section {
                    HStack {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundStyle(.yellow)
                        Text("Configure your MCP server URL in Settings to get started.")
                            .font(.callout)
                    }
                }
            }

            Section("Major Pairs") {
                ForEach(majorPairs) { pair in
                    NavigationLink(destination: ChartContainerView(instrument: pair)) {
                        InstrumentRow(instrument: pair)
                    }
                }
            }

            Section("Cross Pairs") {
                ForEach(crossPairs) { pair in
                    NavigationLink(destination: ChartContainerView(instrument: pair)) {
                        InstrumentRow(instrument: pair)
                    }
                }
            }
        }
        .navigationTitle("Forex Zones")
    }
}

struct InstrumentRow: View {
    let instrument: Instrument

    var body: some View {
        HStack {
            Text(instrument.displayName)
                .font(.headline)
                .monospaced()
            Spacer()
            Image(systemName: "chart.bar.xaxis")
                .foregroundStyle(.secondary)
        }
    }
}

#Preview {
    NavigationStack {
        InstrumentListView()
            .environmentObject(AppSettings())
    }
}
