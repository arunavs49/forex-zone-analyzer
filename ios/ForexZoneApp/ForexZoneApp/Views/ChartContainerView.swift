import SwiftUI

struct ChartContainerView: View {
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @StateObject private var viewModel: ChartViewModel
    @State private var showZoneList = false
    @State private var orderZone: Zone?
    @State private var tradeZone: Zone?

    init(instrument: Instrument) {
        _viewModel = StateObject(wrappedValue: ChartViewModel(instrument: instrument))
    }

    var body: some View {
        VStack(spacing: 0) {
            // Granularity picker
            Picker("Timeframe", selection: $viewModel.granularity) {
                ForEach(Granularity.allCases) { g in
                    Text(g.displayName).tag(g)
                }
            }
            .pickerStyle(.segmented)
            .padding(.horizontal)
            .padding(.vertical, 8)
            .onChange(of: viewModel.granularity) { _, _ in
                Task { await viewModel.loadData(settings: settings, authService: authService) }
            }

            // Info bar: trend + zone count
            if !viewModel.trend.isEmpty {
                HStack(spacing: 8) {
                    TrendBadge(trend: viewModel.trend)

                    Spacer()

                    // Zone counts
                    HStack(spacing: 4) {
                        Circle().fill(.red.opacity(0.6)).frame(width: 6, height: 6)
                        Text("\(viewModel.chartSupplyZones.count)")
                            .font(.caption2.weight(.medium))
                    }
                    HStack(spacing: 4) {
                        Circle().fill(.green.opacity(0.6)).frame(width: 6, height: 6)
                        Text("\(viewModel.chartDemandZones.count)")
                            .font(.caption2.weight(.medium))
                    }
                }
                .padding(.horizontal)
                .padding(.bottom, 4)
            }

            // Chart area
            if viewModel.isLoading {
                Spacer()
                VStack(spacing: 16) {
                    ChartLoadingView()
                    Text("Loading chart data...")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
            } else if let error = viewModel.error {
                ScrollView {
                    VStack(spacing: 12) {
                        Image(systemName: "exclamationmark.triangle")
                            .font(.largeTitle)
                            .foregroundStyle(.red)
                        Text(error)
                            .font(.system(.caption, design: .monospaced))
                            .multilineTextAlignment(.leading)
                            .foregroundStyle(.secondary)
                            .textSelection(.enabled)
                        Button("Retry") {
                            Task { await viewModel.loadData(settings: settings, authService: authService) }
                        }
                        .buttonStyle(.bordered)
                    }
                    .padding()
                }
            } else if viewModel.candles.isEmpty {
                Spacer()
                VStack(spacing: 8) {
                    Image(systemName: "chart.bar.xaxis")
                        .font(.system(size: 40))
                        .foregroundStyle(.tertiary)
                    Text("No data available")
                        .foregroundStyle(.secondary)
                }
                Spacer()
            } else {
                CandlestickChartView(
                    candles: viewModel.candles,
                    supplyZones: viewModel.chartSupplyZones,
                    demandZones: viewModel.chartDemandZones,
                    focusedZone: $viewModel.focusedZone
                )
                .background(Color(red: 0.08, green: 0.09, blue: 0.12))
                .clipShape(RoundedRectangle(cornerRadius: 8))
                .padding(.horizontal, 4)
            }
        }
        .background(Color(.systemBackground))
        .navigationTitle(viewModel.instrument.displayName)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showZoneList = true
                } label: {
                    Image(systemName: "list.bullet.rectangle")
                }
                .disabled(viewModel.allZones.isEmpty)
            }
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    Task { await viewModel.loadData(settings: settings, authService: authService) }
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
            }
        }
        .sheet(isPresented: $showZoneList) {
            ZoneListView(
                supplyZones: viewModel.visibleSupplyZones,
                demandZones: viewModel.visibleDemandZones,
                instrument: viewModel.instrument,
                onZoneTapped: { zone in
                    viewModel.focusedZone = zone  // scrolls chart to zone
                    tradeZone = zone              // shows ZoneFocusBar
                }
            )
        }
        .sheet(item: $orderZone) { zone in
            PlaceOrderSheet(zone: zone, instrument: viewModel.instrument)
        }
        .safeAreaInset(edge: .bottom) {
            if let zone = tradeZone {
                ZoneFocusBar(zone: zone, instrument: viewModel.instrument) {
                    orderZone = zone
                } onDismiss: {
                    tradeZone = nil
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .animation(.spring(response: 0.3), value: tradeZone?.id)
            }
        }
        .task {
            await viewModel.loadData(settings: settings, authService: authService)
        }
    }
}

// MARK: - Focused zone bottom bar

/// A compact bottom bar that appears when the user taps a zone on the chart.
struct ZoneFocusBar: View {
    let zone: Zone
    let instrument: Instrument
    let onPlaceOrder: () -> Void
    let onDismiss: () -> Void

    private var zoneColor: Color { zone.type == .Supply ? .red : .green }
    private var direction: String { zone.type == .Supply ? "Short" : "Long" }

    var body: some View {
        HStack(spacing: 12) {
            // Zone type pill
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(zone.type.rawValue)
                        .font(.caption.weight(.bold))
                        .foregroundStyle(zoneColor)
                    FreshnessBadge(freshness: zone.freshness)
                }
                Text("\(formatPrice(zone.baseRangeLow)) — \(formatPrice(zone.baseRangeHigh))")
                    .font(.system(size: 11, design: .monospaced))
                    .foregroundStyle(.secondary)
            }

            Spacer()

            // Place order button
            Button {
                onPlaceOrder()
            } label: {
                Label("Trade \(direction)", systemImage: zone.type == .Demand ? "arrow.up.circle.fill" : "arrow.down.circle.fill")
                    .font(.caption.weight(.semibold))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(zoneColor)
                    .foregroundStyle(.white)
                    .clipShape(Capsule())
            }

            // Dismiss
            Button {
                onDismiss()
            } label: {
                Image(systemName: "xmark.circle.fill")
                    .foregroundStyle(.secondary)
                    .font(.title3)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
        .background(.ultraThinMaterial)
        .overlay(alignment: .top) {
            Divider()
        }
    }

    private func formatPrice(_ price: Double) -> String {
        price < 10 ? String(format: "%.5f", price) : String(format: "%.3f", price)
    }
}

// MARK: - Animated loading placeholder

struct ChartLoadingView: View {
    @State private var phase: CGFloat = 0

    var body: some View {
        HStack(spacing: 4) {
            ForEach(0..<7, id: \.self) { i in
                RoundedRectangle(cornerRadius: 2)
                    .fill(i % 2 == 0 ? Color.green.opacity(0.4) : Color.red.opacity(0.4))
                    .frame(width: 8, height: barHeight(for: i))
                    .animation(
                        .easeInOut(duration: 0.6)
                        .repeatForever(autoreverses: true)
                        .delay(Double(i) * 0.08),
                        value: phase
                    )
            }
        }
        .onAppear { phase = 1 }
    }

    private func barHeight(for index: Int) -> CGFloat {
        let base: CGFloat = phase == 0 ? 20 : 50
        let variance: CGFloat = phase == 0 ? 0 : CGFloat(index % 3) * 10
        return base + variance
    }
}

// MARK: - Trend badge

struct TrendBadge: View {
    let trend: String

    var body: some View {
        HStack(spacing: 4) {
            Image(systemName: trendIcon)
                .font(.caption2)
            Text(trend.uppercased())
                .font(.system(size: 10, weight: .bold, design: .monospaced))
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 5)
        .background(
            Capsule()
                .fill(trendColor.opacity(0.15))
                .overlay(Capsule().strokeBorder(trendColor.opacity(0.3), lineWidth: 0.5))
        )
        .foregroundStyle(trendColor)
    }

    private var trendIcon: String {
        switch trend.lowercased() {
        case "up": return "arrow.up.right"
        case "down": return "arrow.down.right"
        default: return "arrow.right"
        }
    }

    private var trendColor: Color {
        switch trend.lowercased() {
        case "up": return .green
        case "down": return .red
        default: return .orange
        }
    }
}
