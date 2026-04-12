import Foundation
import UserNotifications
import BackgroundTasks

/// Polls the MCP server for new zones using both foreground timers and iOS Background App Refresh.
/// Foreground: Timer fires at the configured interval while app is active.
/// Background: BGAppRefreshTask wakes the app periodically (iOS controls exact timing, typically ~15-30 min).
@MainActor
class ZonePollingService: ObservableObject {
    nonisolated static let bgTaskIdentifier = "com.zoneradar.app.zone-refresh"

    @Published var lastCheckDate: Date?
    @Published var newZoneCount: Int = 0

    private var timer: Timer?
    private var knownZoneIds: Set<String> = []
    private var isFirstPoll = true

    private let settings: AppSettings
    var authService: AuthService?

    init(settings: AppSettings) {
        self.settings = settings
    }

    // MARK: - Lifecycle

    /// Register the BG task handler — call once at app launch, before the end of applicationDidFinishLaunching.
    nonisolated func registerBackgroundTask() {
        BGTaskScheduler.shared.register(forTaskWithIdentifier: Self.bgTaskIdentifier, using: nil) { task in
            guard let refreshTask = task as? BGAppRefreshTask else { return }
            Task { @MainActor [weak self] in
                await self?.handleBackgroundRefresh(refreshTask)
            }
        }
    }

    func start() {
        requestNotificationPermission()
        scheduleTimer()
        scheduleBackgroundRefresh()
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    func restart() {
        stop()
        scheduleTimer()
        scheduleBackgroundRefresh()
    }

    // MARK: - Foreground timer

    private func scheduleTimer() {
        guard settings.pollEnabled, settings.isConfigured else { return }

        let interval = TimeInterval(settings.pollIntervalMinutes * 60)
        poll()
        timer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.poll()
            }
        }
    }

    private func poll() {
        guard settings.isConfigured else { return }
        Task { await performPoll() }
    }

    // MARK: - Background App Refresh

    func scheduleBackgroundRefresh() {
        guard settings.pollEnabled, settings.isConfigured else { return }

        let request = BGAppRefreshTaskRequest(identifier: Self.bgTaskIdentifier)
        // Ask iOS to wake us no sooner than the user's configured interval
        request.earliestBeginDate = Date(timeIntervalSinceNow: TimeInterval(settings.pollIntervalMinutes * 60))
        do {
            try BGTaskScheduler.shared.submit(request)
            print("[ZonePolling] Scheduled background refresh in ~\(settings.pollIntervalMinutes) min")
        } catch {
            print("[ZonePolling] Failed to schedule background refresh: \(error.localizedDescription)")
        }
    }

    private func handleBackgroundRefresh(_ task: BGAppRefreshTask) async {
        // Schedule the next refresh before doing work
        scheduleBackgroundRefresh()

        // Set expiration handler
        task.expirationHandler = {
            task.setTaskCompleted(success: false)
        }

        await performPoll()
        task.setTaskCompleted(success: true)
    }

    // MARK: - Core polling logic

    private func performPoll() async {
        let dataService = ForexDataService()
        do {
            if let auth = authService, auth.isSignedIn {
                try await dataService.configure(url: settings.mcpServerURL, tokenProvider: { await auth.getAccessToken() })
            } else {
                try await dataService.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }
        } catch {
            print("[ZonePolling] Failed to configure: \(error.localizedDescription)")
            return
        }

        var allNewZones: [(String, Zone)] = []

        for instrument in Instrument.allCases {
            await checkInstrument(instrument, dataService: dataService, newZones: &allNewZones)
        }

        isFirstPoll = false
        lastCheckDate = Date()

        for (instrument, zone) in allNewZones {
            newZoneCount += 1
            sendLocalNotification(instrument: instrument, zone: zone)
        }
    }

    private func checkInstrument(_ instrument: Instrument, dataService: ForexDataService, newZones: inout [(String, Zone)]) async {
        for granularity in Granularity.allCases {
            do {
                let response: ZoneAnalysisResponse = try await dataService.getStoredZones(instrument: instrument.rawValue, granularity: granularity.rawValue)
                let zones: [Zone] = response.supplyZones + response.demandZones
                for zone in zones {
                    let isFresh: Bool = zone.freshness == .Untested || zone.freshness == .Tested
                    guard isFresh else { continue }

                    let zoneId: String = "\(instrument.rawValue)_\(granularity.rawValue)_\(zone.type)_\(zone.startTime)_\(zone.baseRangeLow)"
                    if !knownZoneIds.contains(zoneId) {
                        knownZoneIds.insert(zoneId)
                        if !isFirstPoll {
                            newZones.append((instrument.rawValue, zone))
                        }
                    }
                }
            } catch {
                print("[ZonePolling] Failed to check \(instrument.rawValue) \(granularity.rawValue): \(error.localizedDescription)")
            }
        }
    }

    // MARK: - Notifications

    private func requestNotificationPermission() {
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge]) { granted, error in
            if let error = error {
                print("[ZonePolling] Notification permission error: \(error.localizedDescription)")
            }
            print("[ZonePolling] Notification permission: \(granted)")
        }
    }

    private func sendLocalNotification(instrument: String, zone: Zone) {
        let content = UNMutableNotificationContent()
        content.title = "New \(zone.type) Zone"
        content.subtitle = instrument.replacingOccurrences(of: "_", with: "/")
        content.body = "\(zone.freshness) zone at \(String(format: "%.5f", zone.baseRangeLow)) – \(String(format: "%.5f", zone.baseRangeHigh))"
        content.sound = .default

        let request = UNNotificationRequest(
            identifier: UUID().uuidString,
            content: content,
            trigger: nil
        )

        UNUserNotificationCenter.current().add(request) { error in
            if let error = error {
                print("[ZonePolling] Failed to deliver notification: \(error.localizedDescription)")
            }
        }
    }
}
