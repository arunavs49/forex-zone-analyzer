import Foundation

/// Settings persisted to UserDefaults
class AppSettings: ObservableObject {
    @Published var mcpServerURL: String {
        didSet { UserDefaults.standard.set(mcpServerURL, forKey: "mcpServerURL") }
    }
    @Published var bearerToken: String {
        didSet { UserDefaults.standard.set(bearerToken, forKey: "bearerToken") }
    }
    @Published var nhConnectionString: String {
        didSet { UserDefaults.standard.set(nhConnectionString, forKey: "nhConnectionString") }
    }
    @Published var nhName: String {
        didSet { UserDefaults.standard.set(nhName, forKey: "nhName") }
    }

    init() {
        self.mcpServerURL = UserDefaults.standard.string(forKey: "mcpServerURL")
            ?? "https://your-container-app.azurecontainerapps.io/mcp"
        self.bearerToken = UserDefaults.standard.string(forKey: "bearerToken") ?? ""
        self.nhConnectionString = UserDefaults.standard.string(forKey: "nhConnectionString") ?? ""
        self.nhName = UserDefaults.standard.string(forKey: "nhName") ?? ""
    }

    var isConfigured: Bool {
        !mcpServerURL.isEmpty && (mcpServerURL.hasPrefix("https://") || mcpServerURL.hasPrefix("http://"))
    }

    var isPushConfigured: Bool {
        !nhConnectionString.isEmpty && !nhName.isEmpty
    }
}
