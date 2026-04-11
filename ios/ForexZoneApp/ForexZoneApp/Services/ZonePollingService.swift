import Foundation
import UserNotifications

/// Polls the MCP server for new zones at a configurable interval and fires local notifications.
@MainActor
class ZonePollingService: ObservableObject {
    @Published var lastCheckDate: Date?
    @Published var newZoneCount: Int = 0

    private var timer: Timer?
    private var knownZoneIds: Set<String> = []
    private var isFirstPoll = true

    private let settings: AppSettings

    init(settings: AppSettings) {
        self.settings = settings
    }

    func start() {
        requestNotificationPermission()
        scheduleTimer()
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    func restart() {
        stop()
        scheduleTimer()
    }

    private func scheduleTimer() {
        guard settings.pollEnabled, settings.isConfigured else { return }

        let interval = TimeInterval(settings.pollIntervalMinutes * 60)
        // Fire immediately, then repeat
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

    private func performPoll() async {
        let dataService = ForexDataService()
        do {
            try await dataService.configure(url: settings.mcpServerURL, token: settings.bearerToken)
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
        do {
            let response: ZoneAnalysisResponse = try await dataService.getZones(instrument: instrument.rawValue, granularity: "M15")
            let zones: [Zone] = response.supplyZones + response.demandZones
            for zone in zones {
                let isFresh: Bool = zone.freshness == .Untested || zone.freshness == .Tested
                guard isFresh else { continue }

                let zoneId: String = "\(instrument.rawValue)_\(zone.type)_\(zone.startTime)_\(zone.baseRangeLow)"
                if !knownZoneIds.contains(zoneId) {
                    knownZoneIds.insert(zoneId)
                    if !isFirstPoll {
                        newZones.append((instrument.rawValue, zone))
                    }
                }
            }
        } catch {
            print("[ZonePolling] Failed to check \(instrument.rawValue): \(error.localizedDescription)")
        }
    }

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
            trigger: nil // deliver immediately
        )

        UNUserNotificationCenter.current().add(request) { error in
            if let error = error {
                print("[ZonePolling] Failed to deliver notification: \(error.localizedDescription)")
            }
        }
    }
}
