import SwiftUI

struct SettingsView: View {
    @EnvironmentObject var settings: AppSettings
    @Environment(\.dismiss) private var dismiss

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

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Bearer Token (Entra ID)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        SecureField("Paste token here", text: $settings.bearerToken)
                            .font(.system(.body, design: .monospaced))
                    }
                }

                Section {
                    HStack {
                        Image(systemName: settings.isConfigured ? "checkmark.circle.fill" : "xmark.circle.fill")
                            .foregroundStyle(settings.isConfigured ? .green : .red)
                        Text(settings.isConfigured ? "Server configured" : "Enter a valid http:// or https:// URL")
                            .font(.callout)
                    }
                }

                Section("Zone Alerts (Background Polling)") {
                    Toggle("Enable zone polling", isOn: $settings.pollEnabled)

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
                    Text("This app connects to your Forex Zone Analyzer MCP server deployed on Azure Container Apps. Configure the URL and Entra ID bearer token to fetch candlestick data and visualize supply/demand zones.")
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
}

#Preview {
    SettingsView()
        .environmentObject(AppSettings())
}
