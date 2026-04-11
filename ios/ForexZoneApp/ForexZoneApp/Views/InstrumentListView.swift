import SwiftUI

struct InstrumentListView: View {
    @EnvironmentObject var settings: AppSettings

    private let majorPairs: [Instrument] = [.EUR_USD, .GBP_USD, .USD_JPY, .USD_CHF, .AUD_USD, .NZD_USD, .USD_CAD]
    private let crossPairs: [Instrument] = [.EUR_GBP, .EUR_JPY, .GBP_JPY, .EUR_CHF, .AUD_JPY, .EUR_AUD, .GBP_AUD, .GBP_CHF, .EUR_CAD, .AUD_CAD, .NZD_JPY, .CAD_JPY, .CHF_JPY]

    var body: some View {
        List {
            if !settings.isConfigured {
                Section {
                    HStack(spacing: 12) {
                        Image(systemName: "antenna.radiowaves.left.and.right.slash")
                            .font(.title2)
                            .foregroundStyle(.orange)
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Not Connected")
                                .font(.subheadline.weight(.semibold))
                            Text("Set your MCP server URL in Settings")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                    }
                    .padding(.vertical, 4)
                }
            }

            Section {
                ForEach(majorPairs) { pair in
                    NavigationLink(destination: ChartContainerView(instrument: pair)) {
                        InstrumentRow(instrument: pair)
                    }
                }
            } header: {
                Label("Major Pairs", systemImage: "star.fill")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)
            }

            Section {
                ForEach(crossPairs) { pair in
                    NavigationLink(destination: ChartContainerView(instrument: pair)) {
                        InstrumentRow(instrument: pair)
                    }
                }
            } header: {
                Label("Cross Pairs", systemImage: "arrow.triangle.swap")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle("Forex Zones")
    }
}

struct InstrumentRow: View {
    let instrument: Instrument

    var body: some View {
        HStack(spacing: 12) {
            // Flag pair
            Text(instrument.flagEmoji)
                .font(.title2)

            VStack(alignment: .leading, spacing: 2) {
                Text(instrument.displayName)
                    .font(.system(.body, design: .monospaced, weight: .semibold))
                Text(instrument.fullName)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Spacer()

            Image(systemName: "chart.xyaxis.line")
                .font(.subheadline)
                .foregroundStyle(Color("AccentTeal"))
        }
        .padding(.vertical, 4)
    }
}

// MARK: - Instrument metadata extensions

extension Instrument {
    var flagEmoji: String {
        let flags: [String: String] = [
            "EUR": "🇪🇺", "USD": "🇺🇸", "GBP": "🇬🇧", "JPY": "🇯🇵",
            "CHF": "🇨🇭", "AUD": "🇦🇺", "NZD": "🇳🇿", "CAD": "🇨🇦"
        ]
        let parts = rawValue.split(separator: "_")
        let base = flags[String(parts[0])] ?? "💱"
        let quote = flags[String(parts[1])] ?? "💱"
        return "\(base)\(quote)"
    }

    var fullName: String {
        let names: [String: String] = [
            "EUR": "Euro", "USD": "US Dollar", "GBP": "Pound", "JPY": "Yen",
            "CHF": "Franc", "AUD": "Aussie", "NZD": "Kiwi", "CAD": "Loonie"
        ]
        let parts = rawValue.split(separator: "_")
        let base = names[String(parts[0])] ?? String(parts[0])
        let quote = names[String(parts[1])] ?? String(parts[1])
        return "\(base) / \(quote)"
    }
}

#Preview {
    NavigationStack {
        InstrumentListView()
            .environmentObject(AppSettings())
    }
}
