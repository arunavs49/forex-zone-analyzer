import SwiftUI

/// Latest optimization runs across all currency pairs and timeframes
struct OptimizationRunsView: View {
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @StateObject private var viewModel = OptimizationViewModel()

    var body: some View {
        List {
            if viewModel.isLoading {
                HStack {
                    Spacer()
                    ProgressView("Loading runs...")
                    Spacer()
                }
            } else if let error = viewModel.error {
                VStack(spacing: 8) {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                    Button("Retry") {
                        Task { await viewModel.loadRuns(settings: settings, authService: authService) }
                    }
                    .buttonStyle(.bordered)
                }
            } else if viewModel.runs.isEmpty {
                Text("No optimization runs yet.")
                    .foregroundStyle(.secondary)
            } else {
                ForEach(viewModel.runs) { run in
                    Button {
                        Task { await viewModel.loadRunDetails(run: run, settings: settings, authService: authService) }
                    } label: {
                        RecentRunRow(run: run)
                    }
                    .buttonStyle(.plain)
                }
            }

            if let success = viewModel.successMessage {
                Section {
                    Text(success)
                        .foregroundStyle(.green)
                        .font(.caption)
                }
            }
        }
        .navigationTitle("Optimization Runs")
        .navigationBarTitleDisplayMode(.inline)
        .task {
            await viewModel.loadRuns(settings: settings, authService: authService)
        }
        .refreshable {
            await viewModel.loadRuns(settings: settings, authService: authService)
        }
        .sheet(item: $viewModel.activeRun) { run in
            let summary = viewModel.runs.first { $0.RunId == run.RunId }
            StrategyResultsView(run: run) {
                if let s = summary {
                    Task {
                        await viewModel.applyResult(run: s, settings: settings, authService: authService)
                        viewModel.activeRun = nil
                    }
                }
            }
        }
    }
}

// MARK: - Recent Run Row

struct RecentRunRow: View {
    let run: StrategyRunSummary

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                // Pair + TF label
                Text(pairLabel)
                    .font(.subheadline.weight(.semibold))

                Spacer()

                StatusBadge(status: run.Status ?? "Unknown")
            }

            if run.Status == "Completed" {
                HStack(spacing: 12) {
                    if let winRate = run.BestWinRate {
                        Text("Win: \(String(format: "%.1f%%", winRate * 100))")
                            .font(.caption.weight(.medium))
                            .foregroundStyle(.green)
                    }
                    if let score = run.BestScore {
                        Text("Score: \(String(format: "%.3f", score))")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    if let months = run.LookbackMonths {
                        Text("\(months)mo")
                            .font(.caption2)
                            .foregroundStyle(.tertiary)
                    }
                }
            }

            if let error = run.Error {
                Text(error)
                    .font(.caption2)
                    .foregroundStyle(.red)
                    .lineLimit(1)
            }

            if let time = run.RequestedUtc {
                Text(formatRelativeTime(time))
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
            }
        }
        .padding(.vertical, 2)
    }

    private var pairLabel: String {
        let inst = (run.Instrument ?? "?").replacingOccurrences(of: "_", with: "/")
        let gran = run.Granularity ?? "?"
        return "\(inst) \(gran)"
    }

    private func formatRelativeTime(_ dateString: String) -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        var date = formatter.date(from: dateString)
        if date == nil {
            formatter.formatOptions = [.withInternetDateTime]
            date = formatter.date(from: dateString)
        }
        guard let d = date else { return dateString }

        let interval = Date().timeIntervalSince(d)
        if interval < 60 { return "just now" }
        if interval < 3600 { return "\(Int(interval / 60))m ago" }
        if interval < 86400 { return "\(Int(interval / 3600))h ago" }
        return "\(Int(interval / 86400))d ago"
    }
}
