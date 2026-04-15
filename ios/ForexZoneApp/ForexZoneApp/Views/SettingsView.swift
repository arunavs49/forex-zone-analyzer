import SwiftUI

struct SettingsView: View {
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @Environment(\.dismiss) private var dismiss

    @State private var loadedAccounts: [String] = []
    @State private var isLoadingAccounts = false
    @State private var accountLoadError = ""
    private let service = ForexDataService()

    var body: some View {
        NavigationStack {
            Form {
                Section("MCP Server") {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Server URL")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        TextField("https://your-app.azurecontainerapps.io/mcp", text: $settings.mcpServerURL)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .keyboardType(.URL)
                            .font(.system(.body, design: .monospaced))
                    }
                }

                Section("Authentication") {
                    if authService.isSignedIn {
                        HStack {
                            Image(systemName: "person.crop.circle.fill")
                                .foregroundStyle(.green)
                            VStack(alignment: .leading) {
                                Text("Signed in")
                                    .font(.callout.bold())
                                if let name = authService.userDisplayName {
                                    Text(name)
                                        .font(.caption)
                                        .foregroundStyle(.secondary)
                                }
                            }
                            Spacer()
                            Button("Sign Out", role: .destructive) {
                                authService.signOut()
                            }
                            .buttonStyle(.bordered)
                        }
                    } else {
                        HStack {
                            Image(systemName: "person.crop.circle.badge.xmark")
                                .foregroundStyle(.red)
                            Text("Not signed in")
                                .font(.callout)
                            Spacer()
                            Button("Sign In") {
                                Task { await authService.signIn() }
                            }
                            .buttonStyle(.borderedProminent)
                        }

                        if let error = authService.errorMessage {
                            Text(error)
                                .font(.caption)
                                .foregroundStyle(.red)
                        }
                    }

                    // Fallback manual token for advanced users
                    DisclosureGroup("Manual Token (Advanced)") {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("Override with a manually obtained Entra ID token")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            SecureField("Paste token here", text: $settings.bearerToken)
                                .font(.system(.body, design: .monospaced))
                        }
                    }
                }

                Section {
                    HStack {
                        Image(systemName: (settings.isConfigured && (authService.isSignedIn || !settings.bearerToken.isEmpty))
                              ? "checkmark.circle.fill" : "xmark.circle.fill")
                            .foregroundStyle((settings.isConfigured && (authService.isSignedIn || !settings.bearerToken.isEmpty))
                                            ? .green : .red)
                        Text(statusMessage)
                            .font(.callout)
                    }
                }

                Section("Trading") {
                    // Account picker
                    if loadedAccounts.isEmpty {
                        HStack {
                            VStack(alignment: .leading, spacing: 4) {
                                Text("OANDA Account ID")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                if settings.oandaAccountId.isEmpty {
                                    Text("No account selected")
                                        .foregroundStyle(.secondary)
                                        .font(.callout)
                                } else {
                                    Text(settings.oandaAccountId)
                                        .font(.system(.body, design: .monospaced))
                                }
                            }
                            Spacer()
                            Button {
                                Task { await loadAccounts() }
                            } label: {
                                if isLoadingAccounts {
                                    ProgressView().frame(width: 60)
                                } else {
                                    Text("Load")
                                        .font(.callout)
                                }
                            }
                            .buttonStyle(.bordered)
                            .disabled(isLoadingAccounts || !settings.isConfigured)
                        }
                    } else {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("OANDA Account ID")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            Picker("Account", selection: $settings.oandaAccountId) {
                                ForEach(loadedAccounts, id: \.self) { id in
                                    Text(id).tag(id)
                                }
                            }
                            .pickerStyle(.menu)
                            .labelsHidden()
                        }
                        if !accountLoadError.isEmpty {
                            Text(accountLoadError)
                                .font(.caption)
                                .foregroundStyle(.red)
                        }
                    }

                    if !accountLoadError.isEmpty && loadedAccounts.isEmpty {
                        Text(accountLoadError)
                            .font(.caption)
                            .foregroundStyle(.red)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Risk per Trade (USD)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        HStack {
                            Slider(value: $settings.riskAmountUSD, in: 10...500, step: 10)
                            Text("$\(Int(settings.riskAmountUSD))")
                                .font(.system(.body, design: .monospaced))
                                .frame(width: 52, alignment: .trailing)
                        }
                    }

                    if settings.oandaAccountId.isEmpty {
                        Label("Enter Account ID to enable zone order placement", systemImage: "exclamationmark.triangle.fill")
                            .font(.caption)
                            .foregroundStyle(.orange)
                    }
                }

                Section("Zone Alerts (Background Polling)") {                    Toggle("Enable zone polling", isOn: $settings.pollEnabled)

                    if settings.pollEnabled {
                        Picker("Check interval", selection: $settings.pollIntervalMinutes) {
                            Text("5 min").tag(5)
                            Text("15 min").tag(15)
                            Text("30 min").tag(30)
                            Text("60 min").tag(60)
                        }

                        HStack {
                            Image(systemName: "arrow.clockwise.circle.fill")
                                .foregroundStyle(Color("AccentTeal"))
                            Text("Checks for new zones every \(settings.pollIntervalMinutes) min while app is open")
                                .font(.callout)
                                .foregroundStyle(.secondary)
                        }
                    }
                }

                Section("About") {
                    LabeledContent("App", value: "Forex Zone Analyzer")
                    LabeledContent("Version", value: "1.0.0")
                    LabeledContent("MCP Protocol", value: "2025-03-26")
                }

                Section("Help") {
                    Text("This app connects to your Forex Zone Analyzer MCP server deployed on Azure Container Apps. Sign in with your Microsoft account to authenticate automatically.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
            }
            .navigationTitle("Settings")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
        }
    }

    private var statusMessage: String {
        if !settings.isConfigured { return "Enter a valid http:// or https:// URL" }
        if authService.isSignedIn { return "Connected with Microsoft account" }
        if !settings.bearerToken.isEmpty { return "Using manual token" }
        return "Sign in to authenticate"
    }

    private func loadAccounts() async {
        isLoadingAccounts = true
        accountLoadError = ""
        do {
            if authService.isSignedIn {
                try await service.configure(url: settings.mcpServerURL, tokenProvider: { await authService.getAccessToken() })
            } else {
                try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }
            let accounts = try await service.fetchAccounts()
            loadedAccounts = accounts
            if let first = accounts.first, settings.oandaAccountId.isEmpty {
                settings.oandaAccountId = first
            }
        } catch {
            accountLoadError = error.localizedDescription
        }
        isLoadingAccounts = false
    }
}

#Preview {
    SettingsView()
        .environmentObject(AppSettings())
        .environmentObject(AuthService())
}
