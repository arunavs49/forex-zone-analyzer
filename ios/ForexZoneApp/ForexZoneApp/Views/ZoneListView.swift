import SwiftUI

struct ZoneListView: View {
    let supplyZones: [Zone]
    let demandZones: [Zone]
    let instrument: Instrument
    var onZoneTapped: ((Zone) -> Void)?
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                Section("Supply Zones (\(supplyZones.count))") {
                    if supplyZones.isEmpty {
                        Text("No supply zones detected")
                            .foregroundStyle(.secondary)
                    }
                    ForEach(supplyZones) { zone in
                        Button {
                            dismiss()
                            onZoneTapped?(zone)
                        } label: {
                            ZoneRow(zone: zone, color: .red, instrument: instrument)
                        }
                        .buttonStyle(.plain)
                    }
                }

                Section("Demand Zones (\(demandZones.count))") {
                    if demandZones.isEmpty {
                        Text("No demand zones detected")
                            .foregroundStyle(.secondary)
                    }
                    ForEach(demandZones) { zone in
                        Button {
                            dismiss()
                            onZoneTapped?(zone)
                        } label: {
                            ZoneRow(zone: zone, color: .green, instrument: instrument)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            .navigationTitle("\(instrument.displayName) Zones")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
        }
    }
}

struct ZoneRow: View {
    let zone: Zone
    let color: Color
    let instrument: Instrument

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                // Zone type badge
                Text(zone.type.rawValue)
                    .font(.caption.weight(.bold))
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(color.opacity(0.15))
                    .foregroundStyle(color)
                    .clipShape(Capsule())

                // Freshness badge
                FreshnessBadge(freshness: zone.freshness)

                if zone.subZone {
                    Text("SUB")
                        .font(.system(size: 9, weight: .medium))
                        .padding(.horizontal, 4)
                        .padding(.vertical, 1)
                        .background(.purple.opacity(0.15))
                        .foregroundStyle(.purple)
                        .clipShape(Capsule())
                }

                Spacer()

                // Worked indicator
                if let worked = zone.worked {
                    Image(systemName: worked ? "checkmark.circle.fill" : "xmark.circle.fill")
                        .foregroundStyle(worked ? .green : .red)
                        .font(.caption)
                }

                Image(systemName: "chevron.right")
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
            }

            // Price range
            HStack {
                VStack(alignment: .leading) {
                    Text("Range")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    Text("\(formatPrice(zone.baseRangeLow)) — \(formatPrice(zone.baseRangeHigh))")
                        .font(.system(.caption, design: .monospaced))
                }

                Spacer()

                VStack(alignment: .trailing) {
                    Text("Age")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    Text(zoneAge)
                        .font(.caption)
                }

                VStack(alignment: .trailing) {
                    Text("Width")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    Text(formatPips(zone.baseRangeHigh - zone.baseRangeLow))
                        .font(.system(.caption, design: .monospaced))
                }

                VStack(alignment: .trailing) {
                    Text("Base")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    Text("\(zone.baseCandleCount) candles")
                        .font(.caption)
                }
            }
        }
        .padding(.vertical, 4)
    }

    private var zoneAge: String {
        guard let start = zone.startDate else { return "—" }
        let interval = Date().timeIntervalSince(start)
        let totalMinutes = Int(interval / 60)
        if totalMinutes < 60 { return "now" }
        let totalHours = totalMinutes / 60
        if totalHours < 24 { return "\(totalHours)h" }
        let days = totalHours / 24
        let remainingHours = totalHours % 24
        if remainingHours == 0 { return "\(days)d" }
        return "\(days)d \(remainingHours)h"
    }

    private func formatPrice(_ price: Double) -> String {
        if price < 10 {
            return String(format: "%.5f", price)
        } else {
            return String(format: "%.3f", price)
        }
    }

    private func formatPips(_ value: Double) -> String {
        let pipMultiplier: Double = instrument.rawValue.contains("JPY") ? 100 : 10000
        let pips = value * pipMultiplier
        return String(format: "%.1f pips", pips)
    }
}

struct FreshnessBadge: View {
    let freshness: ZoneFreshness

    var body: some View {
        Text(freshness.rawValue)
            .font(.system(size: 9, weight: .medium))
            .padding(.horizontal, 4)
            .padding(.vertical, 1)
            .background(badgeColor.opacity(0.15))
            .foregroundStyle(badgeColor)
            .clipShape(Capsule())
    }

    private var badgeColor: Color {
        switch freshness {
        case .Untested: return .blue
        case .Tested: return .orange
        case .Broken: return .gray
        }
    }
}
