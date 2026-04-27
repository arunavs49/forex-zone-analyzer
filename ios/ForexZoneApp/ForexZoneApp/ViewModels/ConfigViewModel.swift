import Foundation
import SwiftUI

@MainActor
class ConfigViewModel: ObservableObject {
    @Published var configs: [PairConfig] = []
    @Published var isLoading = false
    @Published var error: String?
    @Published var successMessage: String?

    private let service = ForexDataService()

    func loadConfigs(settings: AppSettings, authService: AuthService? = nil) async {
        isLoading = true
        error = nil

        do {
            if let auth = authService, auth.isSignedIn {
                try await service.configure(url: settings.mcpServerURL, tokenProvider: { await auth.getAccessToken() })
            } else {
                try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }

            let response = try await service.listPairConfigs()
            configs = response.Configs
        } catch {
            self.error = error.localizedDescription
        }

        isLoading = false
    }

    func toggleEnabled(config: PairConfig, settings: AppSettings, authService: AuthService? = nil) async {
        do {
            if let auth = authService, auth.isSignedIn {
                try await service.configure(url: settings.mcpServerURL, tokenProvider: { await auth.getAccessToken() })
            } else {
                try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }

            let result = try await service.setPairEnabled(
                instrument: config.Instrument,
                granularity: config.ZoneGranularity,
                enabled: !config.Enabled
            )

            if result.Error != nil {
                error = result.Error
            } else {
                await loadConfigs(settings: settings, authService: authService)
            }
        } catch {
            self.error = error.localizedDescription
        }
    }

    func toggleEmail(config: PairConfig, settings: AppSettings, authService: AuthService? = nil) async {
        do {
            if let auth = authService, auth.isSignedIn {
                try await service.configure(url: settings.mcpServerURL, tokenProvider: { await auth.getAccessToken() })
            } else {
                try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }

            let result = try await service.setPairEmailEnabled(
                instrument: config.Instrument,
                granularity: config.ZoneGranularity,
                emailEnabled: !config.EmailEnabled
            )

            if result.Error != nil {
                error = result.Error
            } else {
                await loadConfigs(settings: settings, authService: authService)
            }
        } catch {
            self.error = error.localizedDescription
        }
    }

    func updateConfig(
        instrument: String, granularity: String, trendGranularity: String,
        enabled: Bool, emailEnabled: Bool,
        minBaseLength: Int, maxBaseLength: Int,
        minLegInRatio: Double, minLegOutRatio: Double,
        swingLookback: Int, trendCandleCount: Int, minSwingPoints: Int,
        settings: AppSettings, authService: AuthService? = nil
    ) async {
        do {
            if let auth = authService, auth.isSignedIn {
                try await service.configure(url: settings.mcpServerURL, tokenProvider: { await auth.getAccessToken() })
            } else {
                try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }

            let result = try await service.updatePairConfig(
                instrument: instrument, granularity: granularity,
                trendGranularity: trendGranularity,
                enabled: enabled, emailEnabled: emailEnabled,
                minBaseLength: minBaseLength, maxBaseLength: maxBaseLength,
                minLegInRatio: minLegInRatio, minLegOutRatio: minLegOutRatio,
                swingLookback: swingLookback, trendCandleCount: trendCandleCount,
                minSwingPoints: minSwingPoints
            )

            if result.Error != nil {
                error = result.Error
            } else {
                successMessage = "Config updated (v\(result.ConfigVersion ?? 0))"
                await loadConfigs(settings: settings, authService: authService)
            }
        } catch {
            self.error = error.localizedDescription
        }
    }
}
