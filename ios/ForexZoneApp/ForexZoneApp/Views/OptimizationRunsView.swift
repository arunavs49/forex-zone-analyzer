import SwiftUI

/// Latest optimization runs across all currency pairs and timeframes
struct OptimizationRunsView: View {
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @StateObject private var viewModel = OptimizationViewModel()
    @State private var showNewRun = false

    var body: some View {
        List {
            // New run section
            if !viewModel.enabledConfigs.isEmpty {
                Section {
                    Button {
                        showNewRun = true
                    } label: {
                        HStack {
                            Image(systemName: "bolt.fill")
                                .foregroundStyle(.orange)
                            Text("New Optimization Run")
                                .fontWeight(.medium)
                            Spacer()
                            Image(systemName: "chevron.right")
                                .font(.caption)
                                .foregroundStyle(.tertiary)
                        }
                    }
                    .buttonStyle(.plain)
                }
            }

            // Status messages
            if let success = viewModel.successMessage {
                Section {
                    Text(success)
                        .foregroundStyle(.green)
                        .font(.caption)
                }
            }

            // Run history
            Section("Recent Runs") {
                if viewModel.isLoading {
                    HStack {
                        Spacer()
                        ProgressView("Loading...")
                        Spacer()
                    }
                } else if let error = viewModel.error {
                    VStack(spacing: 8) {
                        Text(error)
                            .font(.caption)
                            .foregroundStyle(.red)
                        Button("Retry") {
                            Task { await viewModel.loadData(settings: settings, authService: authService) }
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
            }
        }
        .navigationTitle("Optimization Runs")
        .navigationBarTitleDisplayMode(.inline)
        .task {
            await viewModel.loadData(settings: settings, authService: authService)
        }
        .refreshable {
            await viewModel.loadData(settings: settings, authService: authService)
        }
        .sheet(isPresented: $showNewRun) {
            NewRunSheet(viewModel: viewModel)
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

// MARK: - New Run Sheet

struct NewRunSheet: View {
    @ObservedObject var viewModel: OptimizationViewModel
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @Environment(\.dismiss) var dismiss
    @State private var selectedConfigIndex = 0
    @State private var lookbackMonths = 6

    var body: some View {
        NavigationStack {
            Form {
                Section("Pair + Timeframe") {
                    Picker("Config", selection: $selectedConfigIndex) {
                        ForEach(Array(viewModel.enabledConfigs.enumerated()), id: \.offset) { idx, config in
                            Text("\(config.Instrument.replacingOccurrences(of: "_", with: "/")) \(config.ZoneGranularity)")
                                .tag(idx)
                        }
                    }
                    .pickerStyle(.wheel)
                    .frame(height: 120)
                }

                Section("Settings") {
                    Stepper("Lookback: \(lookbackMonths) months", value: $lookbackMonths, in: 1...24)
                }

                if let error = viewModel.error {
                    Section {
                        Text(error).foregroundStyle(.red).font(.caption)
                    }
                }
            }
            .navigationTitle("New Optimization")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Start") {
                        Task {
                            let config = viewModel.enabledConfigs[selectedConfigIndex]
                            await viewModel.startRun(
                                config: config, lookbackMonths: lookbackMonths,
                                settings: settings, authService: authService)
                            if viewModel.error == nil {
                                dismiss()
                            }
                        }
                    }
                    .disabled(viewModel.isStarting || viewModel.enabledConfigs.isEmpty)
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
