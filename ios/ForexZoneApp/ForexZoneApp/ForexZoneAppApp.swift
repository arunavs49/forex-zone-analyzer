import SwiftUI

@main
struct ForexZoneAppApp: App {
    @StateObject private var settings = AppSettings()
    @StateObject private var pollingService: ZonePollingService
    @Environment(\.scenePhase) private var scenePhase

    init() {
        let s = AppSettings()
        _settings = StateObject(wrappedValue: s)
        let service = ZonePollingService(settings: s)
        _pollingService = StateObject(wrappedValue: service)
        // Must register BG task handler before app finishes launching
        service.registerBackgroundTask()
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(settings)
                .environmentObject(pollingService)
                .preferredColorScheme(.dark)
                .tint(Color("AccentTeal"))
                .onChange(of: scenePhase) { _, newPhase in
                    switch newPhase {
                    case .active:
                        pollingService.start()
                    case .background:
                        // Stop foreground timer, ensure background refresh is scheduled
                        pollingService.stop()
                        pollingService.scheduleBackgroundRefresh()
                    case .inactive:
                        break
                    @unknown default:
                        break
                    }
                }
        }
    }
}
