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

            // Trend badge
            if !viewModel.trend.isEmpty {
                HStack {
                    TrendBadge(trend: viewModel.trend)
                    Spacer()
                    Text("\(viewModel.visibleZones.count) visible zones")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .padding(.horizontal)
                .padding(.bottom, 4)
            }

            // Chart
            if viewModel.isLoading {
                Spacer()
                ProgressView("Loading chart data...")
                    .progressViewStyle(.circular)
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
                Text("No data available")
                    .foregroundStyle(.secondary)
                Spacer()
            } else {
                CandlestickChartView(
                    candles: viewModel.candles,
                    supplyZones: viewModel.visibleSupplyZones,
                    demandZones: viewModel.visibleDemandZones,
                    focusedZone: $viewModel.focusedZone
                )
            }
        }
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
                supplyZones: viewModel.supplyZones,
                demandZones: viewModel.demandZones,
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

struct TrendBadge: View {
    let trend: String

    var body: some View {
        HStack(spacing: 4) {
            Image(systemName: trendIcon)
            Text(trend)
                .font(.caption.weight(.semibold))
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .background(trendColor.opacity(0.15))
        .foregroundStyle(trendColor)
        .clipShape(Capsule())
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
