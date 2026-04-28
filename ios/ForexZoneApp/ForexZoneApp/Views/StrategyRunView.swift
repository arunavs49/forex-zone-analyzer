import SwiftUI

/// Strategy optimization view — start runs, view results, apply configs
struct StrategyRunView: View {
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @StateObject private var viewModel: StrategyViewModel
    @State private var lookbackMonths = 6
    @State private var selectedRunId: String?

    init(instrument: String, granularity: String) {
        _viewModel = StateObject(wrappedValue: StrategyViewModel(
            instrument: instrument, granularity: granularity))
    }

    var body: some View {
        List {
            // Start new run section
            Section("New Optimization") {
                Stepper("Lookback: \(lookbackMonths) months", value: $lookbackMonths, in: 1...24)

                Button {
                    Task { await viewModel.startRun(lookbackMonths: lookbackMonths, settings: settings, authService: authService) }
                } label: {
                    HStack {
                        Image(systemName: "bolt.fill")
                        Text("Start Optimization")
                    }
                }
                .disabled(viewModel.isStarting)
            }

            // Status messages
            if let error = viewModel.error {
                Section {
                    Text(error).foregroundStyle(.red).font(.caption)
                }
            }
            if let success = viewModel.successMessage {
                Section {
                    Text(success).foregroundStyle(.green).font(.caption)
                }
            }

            // Run history
            Section("Run History") {
                if viewModel.isLoading {
                    ProgressView("Loading runs...")
                } else if viewModel.runs.isEmpty {
                    Text("No optimization runs yet.")
                        .foregroundStyle(.secondary)
                } else {
                    ForEach(viewModel.runs) { run in
                        Button {
                            if let runId = run.RunId {
                                selectedRunId = runId
                                Task { await viewModel.loadRunDetails(runId: runId, settings: settings, authService: authService) }
                            }
                        } label: {
                            StrategyRunRow(run: run)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
        .navigationTitle("Optimize \(viewModel.instrument.replacingOccurrences(of: "_", with: "/")) \(viewModel.granularity)")
        .navigationBarTitleDisplayMode(.inline)
        .task {
            await viewModel.loadRuns(settings: settings, authService: authService)
        }
        .refreshable {
            await viewModel.loadRuns(settings: settings, authService: authService)
        }
        .sheet(item: $viewModel.activeRun) { run in
            StrategyResultsView(run: run) {
                Task {
                    await viewModel.applyResult(runId: run.RunId, settings: settings, authService: authService)
                    viewModel.activeRun = nil
                }
            }
        }
    }
}

// MARK: - Run row

struct StrategyRunRow: View {
    let run: StrategyRunSummary

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                StatusBadge(status: run.Status ?? "Unknown")
                Spacer()
                if let time = run.RequestedUtc {
                    Text(formatRelativeTime(time))
                        .font(.caption2)
                        .foregroundStyle(.tertiary)
                }
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
        }
        .padding(.vertical, 2)
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

struct StatusBadge: View {
    let status: String

    var body: some View {
        Text(status)
            .font(.caption2.weight(.bold))
            .padding(.horizontal, 8)
            .padding(.vertical, 2)
            .background(statusColor.opacity(0.15))
            .foregroundStyle(statusColor)
            .clipShape(Capsule())
    }

    private var statusColor: Color {
        switch status {
        case "Completed": return .green
        case "Running": return .blue
        case "Queued": return .orange
        case "Failed": return .red
        default: return .gray
        }
    }
}

// MARK: - Results sheet

struct StrategyResultsView: View {
    let run: StrategyRun
    let onApply: () -> Void
    @Environment(\.dismiss) var dismiss

    var body: some View {
        NavigationStack {
            List {
                // Summary
                Section("Summary") {
                    LabeledContent("Score", value: String(format: "%.4f", run.BestScore ?? 0))
                    LabeledContent("Win Rate", value: String(format: "%.1f%%", (run.BestWinRate ?? 0) * 100))
                    LabeledContent("Traded Zones", value: "\(run.BestTradedZones ?? 0)")
                    LabeledContent("Avg RR", value: String(format: "%.2f", run.BestAvgRR ?? 0))
                    LabeledContent("Combos Tested", value: "\(run.TotalCombos ?? 0)")
                    LabeledContent("Combos Scored", value: "\(run.ScoredCombos ?? 0)")
                }

                // Best zone config
                if let zone = run.BestZoneConfig {
                    Section("Best Zone Config") {
                        LabeledContent("Min Base Length", value: "\(zone.MinBaseLength ?? 0)")
                        LabeledContent("Max Base Length", value: "\(zone.MaxBaseLength ?? 0)")
                        LabeledContent("Min Leg-In Ratio", value: String(format: "%.2f", zone.MinLegInToBaseRangeRatio ?? 0))
                        LabeledContent("Min Leg-Out Ratio", value: String(format: "%.2f", zone.MinLegOutToBaseRangeRatio ?? 0))
                    }
                }

                // Best trend config
                if let trend = run.BestTrendConfig {
                    Section("Best Trend Config") {
                        LabeledContent("Swing Lookback", value: "\(trend.SwingLookback ?? 0)")
                        LabeledContent("Candle Count", value: "\(trend.TrendCandleCount ?? 0)")
                        LabeledContent("Min Swing Points", value: "\(trend.MinSwingPoints ?? 0)")
                    }
                }

                // Top results
                if let topResults = run.TopResults, !topResults.isEmpty {
                    Section("Top Configurations") {
                        ForEach(Array(topResults.enumerated()), id: \.offset) { idx, result in
                            DisclosureGroup {
                                // Zone config details
                                if let zone = result.Zone {
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text("Zone Config")
                                            .font(.caption2.weight(.semibold))
                                            .foregroundStyle(.secondary)
                                        HStack(spacing: 12) {
                                            Text("Base: \(zone.MinBaseLength ?? 0)-\(zone.MaxBaseLength ?? 0)")
                                            Text("LegIn: \(String(format: "%.2f", zone.MinLegInToBaseRangeRatio ?? 0))")
                                            Text("LegOut: \(String(format: "%.2f", zone.MinLegOutToBaseRangeRatio ?? 0))")
                                        }
                                        .font(.system(size: 10, design: .monospaced))
                                    }
                                }

                                // Trend config details
                                if let trend = result.Trend {
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text("Trend Config")
                                            .font(.caption2.weight(.semibold))
                                            .foregroundStyle(.secondary)
                                        HStack(spacing: 12) {
                                            Text("Swing: \(trend.SwingLookback ?? 0)")
                                            Text("Candles: \(trend.TrendCandleCount ?? 0)")
                                            Text("MinPts: \(trend.MinSwingPoints ?? 0)")
                                        }
                                        .font(.system(size: 10, design: .monospaced))
                                    }
                                }
                            } label: {
                                VStack(alignment: .leading, spacing: 2) {
                                    HStack {
                                        Text("#\(idx + 1)")
                                            .font(.caption.weight(.bold))
                                        Spacer()
                                        Text(String(format: "Score: %.4f", result.Score))
                                            .font(.caption)
                                    }
                                    HStack(spacing: 8) {
                                        Text("Win: \(String(format: "%.1f%%", result.WinRate * 100))")
                                        Text("Zones: \(result.TradedZones)")
                                        Text("RR: \(String(format: "%.2f", result.AverageRR))")
                                        Text("W:\(result.Wins) L:\(result.Losses) T:\(result.Timeouts)")
                                    }
                                    .font(.system(size: 10, design: .monospaced))
                                    .foregroundStyle(.secondary)
                                }
                                .padding(.vertical, 2)
                            }
                        }
                    }
                }

                // Apply button
                Section {
                    Button {
                        onApply()
                    } label: {
                        HStack {
                            Image(systemName: "checkmark.circle.fill")
                            Text("Apply Best Config")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                }
            }
            .navigationTitle("Run Results")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Close") { dismiss() }
                }
            }
        }
    }
}
