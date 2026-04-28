import SwiftUI

struct ContentView: View {
    @EnvironmentObject var settings: AppSettings
    @State private var showSettings = false
    @State private var showPendingOrders = false
    @State private var showPairConfigs = false
    @State private var showOptimizations = false
    @State private var navigationPath = NavigationPath()

    var body: some View {
        NavigationStack(path: $navigationPath) {
            InstrumentListView()
                .toolbar {
                    ToolbarItem(placement: .topBarLeading) {
                        HStack(spacing: 12) {
                            Button {
                                showPendingOrders = true
                            } label: {
                                Image(systemName: "list.bullet.clipboard")
                            }
                            Button {
                                showPairConfigs = true
                            } label: {
                                Image(systemName: "slider.horizontal.3")
                            }
                            Button {
                                showOptimizations = true
                            } label: {
                                Image(systemName: "chart.bar.xaxis.ascending")
                            }
                        }
                    }
                    ToolbarItem(placement: .topBarTrailing) {
                        Button {
                            showSettings = true
                        } label: {
                            Image(systemName: "gear")
                        }
                    }
                }
                .navigationDestination(for: ChartDestination.self) { dest in
                    ChartContainerView(instrument: dest.instrument, granularity: dest.granularity)
                }
                .sheet(isPresented: $showSettings) {
                    SettingsView()
                }
                .sheet(isPresented: $showPendingOrders) {
                    PendingOrdersView()
                }
                .sheet(isPresented: $showPairConfigs) {
                    NavigationStack {
                        PairConfigListView { instrument, granularity in
                            showPairConfigs = false
                            navigationPath.append(ChartDestination(instrument: instrument, granularity: granularity))
                        }
                    }
                }
                .sheet(isPresented: $showOptimizations) {
                    NavigationStack {
                        OptimizationRunsView()
                    }
                }
        }
    }
}

/// Navigation destination for chart view from config tap
struct ChartDestination: Hashable {
    let instrument: Instrument
    let granularity: Granularity
}

#Preview {
    ContentView()
        .environmentObject(AppSettings())
}
