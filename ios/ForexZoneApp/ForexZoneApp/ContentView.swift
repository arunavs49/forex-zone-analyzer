import SwiftUI

struct ContentView: View {
    @EnvironmentObject var settings: AppSettings
    @State private var showSettings = false
    @State private var showPendingOrders = false

    var body: some View {
        NavigationStack {
            InstrumentListView()
                .toolbar {
                    ToolbarItem(placement: .topBarLeading) {
                        Button {
                            showPendingOrders = true
                        } label: {
                            Image(systemName: "list.bullet.clipboard")
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
        }
    }
}

#Preview {
    ContentView()
        .environmentObject(AppSettings())
}
