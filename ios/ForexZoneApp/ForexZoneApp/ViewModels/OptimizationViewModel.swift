import Foundation
import SwiftUI

/// ViewModel for the cross-pair optimization runs view
@MainActor
class OptimizationViewModel: ObservableObject {
    @Published var runs: [StrategyRunSummary] = []
    @Published var enabledConfigs: [PairConfig] = []
    @Published var activeRun: StrategyRun?
    @Published var isLoading = false
    @Published var isStarting = false
    @Published var error: String?
    @Published var successMessage: String?

    private let service = ForexDataService()

    func loadData(settings: AppSettings, authService: AuthService? = nil) async {
        isLoading = true
        error = nil

        do {
            try await configureService(settings: settings, authService: authService)

            async let runsFetch = service.listRecentStrategyRuns(limit: 10)
            async let configsFetch = service.listPairConfigs()

            let (runsResponse, configsResponse) = try await (runsFetch, configsFetch)
            runs = runsResponse.Runs
            enabledConfigs = configsResponse.Configs.filter { $0.Enabled }
        } catch {
            self.error = error.localizedDescription
        }

        isLoading = false
    }

    func startRun(instrument: String, granularity: String, lookbackMonths: Int, settings: AppSettings, authService: AuthService? = nil) async {
        isStarting = true
        error = nil
        successMessage = nil

        do {
            try await configureService(settings: settings, authService: authService)
            let response = try await service.startStrategyRun(
                instrument: instrument, granularity: granularity,
                lookbackMonths: lookbackMonths)

            if let err = response.Error {
                error = err
            } else {
                let label = "\(instrument.replacingOccurrences(of: "_", with: "/")) \(granularity)"
                successMessage = "Queued optimization for \(label)"
                await loadData(settings: settings, authService: authService)
            }
        } catch {
            self.error = error.localizedDescription
        }

        isStarting = false
    }

    func loadRunDetails(run: StrategyRunSummary, settings: AppSettings, authService: AuthService? = nil) async {
        guard let runId = run.RunId,
              let instrument = run.Instrument,
              let granularity = run.Granularity else { return }

        do {
            try await configureService(settings: settings, authService: authService)
            activeRun = try await service.getStrategyRun(
                instrument: instrument, granularity: granularity, runId: runId)
        } catch {
            self.error = error.localizedDescription
        }
    }

    func applyResult(run: StrategyRunSummary, settings: AppSettings, authService: AuthService? = nil) async {
        guard let runId = run.RunId,
              let instrument = run.Instrument,
              let granularity = run.Granularity else { return }

        do {
            try await configureService(settings: settings, authService: authService)
            let response = try await service.applyStrategyResult(
                instrument: instrument, granularity: granularity, runId: runId)

            if let err = response.Error {
                error = err
            } else {
                successMessage = "Applied config v\(response.ConfigVersion ?? 0) to \(instrument.replacingOccurrences(of: "_", with: "/")) \(granularity)"
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
