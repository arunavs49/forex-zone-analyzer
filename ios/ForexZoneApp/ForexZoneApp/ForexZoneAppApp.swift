import SwiftUI

class AppDelegate: NSObject, UIApplicationDelegate {
    func application(_ application: UIApplication,
                     didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        NotificationService.shared.didRegisterForRemoteNotifications(deviceToken: deviceToken)
    }

    func application(_ application: UIApplication,
                     didFailToRegisterForRemoteNotificationsWithError error: Error) {
        NotificationService.shared.didFailToRegisterForRemoteNotifications(error: error)
    }
}

@main
struct ForexZoneAppApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) var appDelegate
    @StateObject private var settings = AppSettings()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(settings)
                .environmentObject(NotificationService.shared)
                .preferredColorScheme(.dark)
                .tint(Color("AccentTeal"))
                .onAppear {
                    // Configure notification hub from settings
                    NotificationService.shared.configure(
                        connectionString: settings.nhConnectionString,
                        hubName: settings.nhName
                    )
                    NotificationService.shared.requestAuthorization()
                }
        }
    }
}
