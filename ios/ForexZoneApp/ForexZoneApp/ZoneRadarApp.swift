import SwiftUI

@main
struct ZoneRadarApp: App {
    @StateObject private var settings = AppSettings()
    @StateObject private var authService = AuthService()
    @StateObject private var pollingService: ZonePollingService
    @Environment(\.scenePhase) private var scenePhase

    init() {
        let s = AppSettings()
        _settings = StateObject(wrappedValue: s)
        let auth = AuthService()
        _authService = StateObject(wrappedValue: auth)
        let service = ZonePollingService(settings: s)
        service.authService = auth
        _pollingService = StateObject(wrappedValue: service)
        service.registerBackgroundTask()
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(settings)
                .environmentObject(authService)
                .environmentObject(pollingService)
                .preferredColorScheme(.dark)
                .tint(Color("AccentTeal"))
                .onOpenURL { url in
                    authService.handleRedirectURL(url)
                }
                .onChange(of: scenePhase) { _, newPhase in
                    switch newPhase {
                    case .active:
                        pollingService.start()
                    case .background:
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
