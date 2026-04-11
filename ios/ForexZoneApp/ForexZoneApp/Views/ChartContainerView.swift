import SwiftUI

struct ChartContainerView: View {
    @EnvironmentObject var settings: AppSettings
    @StateObject private var viewModel: ChartViewModel
    @State private var showZoneList = false

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
                Task { await viewModel.loadData(settings: settings) }
            }

            // Info bar: trend + zone count
            if !viewModel.trend.isEmpty {
                HStack(spacing: 8) {
                    TrendBadge(trend: viewModel.trend)

                    Spacer()

                    // Zone counts
                    HStack(spacing: 4) {
                        Circle().fill(.red.opacity(0.6)).frame(width: 6, height: 6)
                        Text("\(viewModel.visibleSupplyZones.count)")
                            .font(.caption2.weight(.medium))
                    }
                    HStack(spacing: 4) {
                        Circle().fill(.green.opacity(0.6)).frame(width: 6, height: 6)
                        Text("\(viewModel.visibleDemandZones.count)")
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
                            Task { await viewModel.loadData(settings: settings) }
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
                    supplyZones: viewModel.visibleSupplyZones,
                    demandZones: viewModel.visibleDemandZones,
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
                    Task { await viewModel.loadData(settings: settings) }
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
                    viewModel.focusedZone = zone
                }
            )
        }
        .task {
            await viewModel.loadData(settings: settings)
        }
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
