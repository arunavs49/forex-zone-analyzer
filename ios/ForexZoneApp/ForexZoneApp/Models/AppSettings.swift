import Foundation

/// Settings persisted to UserDefaults
class AppSettings: ObservableObject {
    @Published var mcpServerURL: String {
        didSet { UserDefaults.standard.set(mcpServerURL, forKey: "mcpServerURL") }
    }
    @Published var bearerToken: String {
        didSet { UserDefaults.standard.set(bearerToken, forKey: "bearerToken") }
    }

    init() {
        self.mcpServerURL = UserDefaults.standard.string(forKey: "mcpServerURL")
            ?? "https://your-container-app.azurecontainerapps.io/mcp"
        self.bearerToken = UserDefaults.standard.string(forKey: "bearerToken") ?? ""
    }

    var isConfigured: Bool {
        !mcpServerURL.isEmpty && (mcpServerURL.hasPrefix("https://") || mcpServerURL.hasPrefix("http://"))
    }
}
