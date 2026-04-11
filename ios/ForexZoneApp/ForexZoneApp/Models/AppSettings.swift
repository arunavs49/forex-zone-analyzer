import Foundation

/// Settings persisted to UserDefaults
class AppSettings: ObservableObject {
    @Published var mcpServerURL: String {
        didSet { UserDefaults.standard.set(mcpServerURL, forKey: "mcpServerURL") }
    }
    @Published var bearerToken: String {
        didSet { UserDefaults.standard.set(bearerToken, forKey: "bearerToken") }
    }
    @Published var pollEnabled: Bool {
        didSet { UserDefaults.standard.set(pollEnabled, forKey: "pollEnabled") }
    }
    @Published var pollIntervalMinutes: Int {
        didSet { UserDefaults.standard.set(pollIntervalMinutes, forKey: "pollIntervalMinutes") }
    }

    init() {
        self.mcpServerURL = UserDefaults.standard.string(forKey: "mcpServerURL")
            ?? "https://your-container-app.azurecontainerapps.io/mcp"
        self.bearerToken = UserDefaults.standard.string(forKey: "bearerToken") ?? ""
        self.pollEnabled = UserDefaults.standard.object(forKey: "pollEnabled") as? Bool ?? true
        self.pollIntervalMinutes = UserDefaults.standard.object(forKey: "pollIntervalMinutes") as? Int ?? 15
    }

    var isConfigured: Bool {
        !mcpServerURL.isEmpty && (mcpServerURL.hasPrefix("https://") || mcpServerURL.hasPrefix("http://"))
    }
}
