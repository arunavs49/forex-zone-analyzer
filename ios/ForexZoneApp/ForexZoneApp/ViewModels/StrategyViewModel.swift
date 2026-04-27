import Foundation
import SwiftUI

@MainActor
class StrategyViewModel: ObservableObject {
    @Published var runs: [StrategyRunSummary] = []
    @Published var activeRun: StrategyRun?
    @Published var isLoading = false
    @Published var isStarting = false
    @Published var error: String?
    @Published var successMessage: String?

    let instrument: String
    let granularity: String

    private let service = ForexDataService()

    init(instrument: String, granularity: String) {
        self.instrument = instrument
        self.granularity = granularity
    }

    func loadRuns(settings: AppSettings, authService: AuthService? = nil) async {
        isLoading = true
        error = nil

        do {
            try await configureService(settings: settings, authService: authService)
            let response = try await service.listStrategyRuns(instrument: instrument, granularity: granularity)
            runs = response.Runs
        } catch {
            self.error = error.localizedDescription
        }

        isLoading = false
    }

    func startRun(lookbackMonths: Int, settings: AppSettings, authService: AuthService? = nil) async {
        isStarting = true
        error = nil

        do {
            try await configureService(settings: settings, authService: authService)
            let response = try await service.startStrategyRun(
                instrument: instrument, granularity: granularity, lookbackMonths: lookbackMonths)

            if let err = response.Error {
                error = err
            } else {
                successMessage = "Optimization queued (Run: \(response.RunId ?? "?"))"
                await loadRuns(settings: settings, authService: authService)
            }
        } catch {
            self.error = error.localizedDescription
        }

        isStarting = false
    }

    func loadRunDetails(runId: String, settings: AppSettings, authService: AuthService? = nil) async {
        do {
            try await configureService(settings: settings, authService: authService)
            activeRun = try await service.getStrategyRun(
                instrument: instrument, granularity: granularity, runId: runId)
        } catch {
            self.error = error.localizedDescription
        }
    }

    func applyResult(runId: String, settings: AppSettings, authService: AuthService? = nil) async {
        do {
            try await configureService(settings: settings, authService: authService)
            let response = try await service.applyStrategyResult(
                instrument: instrument, granularity: granularity, runId: runId)

            if let err = response.Error {
                error = err
            } else {
                successMessage = "Applied config v\(response.ConfigVersion ?? 0)"
            }
        } catch {
            self.error = error.localizedDescription
        }
    }

    private func configureService(settings: AppSettings, authService: AuthService?) async throws {
        if let auth = authService, auth.isSignedIn {
            try await service.configure(url: settings.mcpServerURL, tokenProvider: { await auth.getAccessToken() })
        } else {
            try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
        }
    }
}
