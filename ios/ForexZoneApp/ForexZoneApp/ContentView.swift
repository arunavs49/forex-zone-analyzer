import SwiftUI

struct ContentView: View {
    @EnvironmentObject var settings: AppSettings
    @State private var showSettings = false

    var body: some View {
        NavigationStack {
            InstrumentListView()
                .toolbar {
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
        }
    }
}

#Preview {
    ContentView()
        .environmentObject(AppSettings())
}
