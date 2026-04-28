import SwiftUI

struct ContentView: View {
    @EnvironmentObject var settings: AppSettings
    @State private var showSettings = false
    @State private var showPendingOrders = false
    @State private var showPairConfigs = false
    @State private var showOptimizations = false

    var body: some View {
        NavigationStack {
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
                .sheet(isPresented: $showSettings) {
                    SettingsView()
                }
                .sheet(isPresented: $showPendingOrders) {
                    PendingOrdersView()
                }
                .sheet(isPresented: $showPairConfigs) {
                    NavigationStack {
                        PairConfigListView()
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

#Preview {
    ContentView()
        .environmentObject(AppSettings())
}
