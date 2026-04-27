import SwiftUI

/// List of all pair+TF configurations with enable/disable toggles
struct PairConfigListView: View {
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @StateObject private var viewModel = ConfigViewModel()
    @State private var editingConfig: PairConfig?
    @State private var showAddConfig = false

    var body: some View {
        List {
            if viewModel.isLoading {
                HStack {
                    Spacer()
                    ProgressView("Loading configs...")
                    Spacer()
                }
            } else if let error = viewModel.error {
                VStack(spacing: 8) {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                    Button("Retry") {
                        Task { await viewModel.loadConfigs(settings: settings, authService: authService) }
                    }
                    .buttonStyle(.bordered)
                }
            } else if viewModel.configs.isEmpty {
                Text("No pair configurations found.")
                    .foregroundStyle(.secondary)
            } else {
                ForEach(groupedConfigs, id: \.key) { instrument, configs in
                    Section(header: Text(instrument.replacingOccurrences(of: "_", with: "/"))) {
                        ForEach(configs) { config in
                            PairConfigRow(config: config) {
                                Task { await viewModel.toggleEnabled(config: config, settings: settings, authService: authService) }
                            } onToggleEmail: {
                                Task { await viewModel.toggleEmail(config: config, settings: settings, authService: authService) }
                            }
                            .swipeActions(edge: .leading) {
                                Button {
                                    editingConfig = config
                                } label: {
                                    Label("Edit", systemImage: "pencil")
                                }
                                .tint(.orange)
                            }
                        }
                    }
                }
            }
        }
        .navigationTitle("Pair Configs")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showAddConfig = true
                } label: {
                    Image(systemName: "plus")
                }
            }
        }
        .task {
            await viewModel.loadConfigs(settings: settings, authService: authService)
        }
        .refreshable {
            await viewModel.loadConfigs(settings: settings, authService: authService)
        }
        .sheet(item: $editingConfig) { config in
            PairConfigEditView(config: config, viewModel: viewModel)
        }
        .sheet(isPresented: $showAddConfig) {
            AddPairConfigView(viewModel: viewModel)
        }
        .navigationDestination(for: PairConfig.self) { config in
            StrategyRunView(instrument: config.Instrument, granularity: config.ZoneGranularity)
        }
    }

    private var groupedConfigs: [(key: String, value: [PairConfig])] {
        Dictionary(grouping: viewModel.configs, by: { $0.Instrument })
            .sorted { $0.key < $1.key }
    }
}

// MARK: - Config Row

struct PairConfigRow: View {
    let config: PairConfig
    let onToggleEnabled: () -> Void
    let onToggleEmail: () -> Void

    var body: some View {
        NavigationLink(value: config) {
            VStack(alignment: .leading, spacing: 6) {
                HStack {
                    Text(config.ZoneGranularity)
                        .font(.headline)

                    Spacer()

                    // Enabled toggle
                    Button {
                        onToggleEnabled()
                    } label: {
                        Text(config.Enabled ? "Enabled" : "Disabled")
                            .font(.caption.weight(.semibold))
                            .padding(.horizontal, 8)
                            .padding(.vertical, 3)
                            .background(config.Enabled ? Color.green.opacity(0.15) : Color.gray.opacity(0.15))
                            .foregroundStyle(config.Enabled ? .green : .secondary)
                            .clipShape(Capsule())
                    }
                    .buttonStyle(.plain)

                    // Email toggle
                    Button {
                        onToggleEmail()
                    } label: {
                        Image(systemName: config.EmailEnabled ? "envelope.fill" : "envelope")
                            .font(.caption)
                            .foregroundStyle(config.EmailEnabled ? .blue : .secondary)
                    }
                    .buttonStyle(.plain)
                }

            // Status info
            HStack(spacing: 12) {
                if let trend = config.Trend {
                    TrendBadge(trend: trend)
                }

                if let zoneCount = config.ZoneCount {
                    Text("\(zoneCount) zones")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                if let lastProcessed = config.LastProcessedUtc {
                    Text(formatRelativeTime(lastProcessed))
                        .font(.caption2)
                        .foregroundStyle(.tertiary)
                }
            }

            // Config summary
            HStack(spacing: 8) {
                Text("Trend: \(config.TrendGranularity)")
                Text("Base: \(config.MinBaseLength ?? 1)-\(config.MaxBaseLength ?? 6)")
                Text("v\(config.ConfigVersion)")
            }
            .font(.system(size: 10, design: .monospaced))
            .foregroundStyle(.tertiary)
        }
            .padding(.vertical, 2)
        } // NavigationLink
    }

    private func formatRelativeTime(_ dateString: String) -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        guard let date = formatter.date(from: dateString) else {
            // Try without fractional seconds
            formatter.formatOptions = [.withInternetDateTime]
            guard let date = formatter.date(from: dateString) else { return dateString }
            return relativeString(from: date)
        }
        return relativeString(from: date)
    }

    private func relativeString(from date: Date) -> String {
        let interval = Date().timeIntervalSince(date)
        if interval < 60 { return "just now" }
        if interval < 3600 { return "\(Int(interval / 60))m ago" }
        if interval < 86400 { return "\(Int(interval / 3600))h ago" }
        return "\(Int(interval / 86400))d ago"
    }
}

// MARK: - Config Edit Sheet

struct PairConfigEditView: View {
    let config: PairConfig
    @ObservedObject var viewModel: ConfigViewModel
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @Environment(\.dismiss) var dismiss

    @State private var enabled: Bool
    @State private var emailEnabled: Bool
    @State private var trendGranularity: String
    @State private var minBaseLength: Int
    @State private var maxBaseLength: Int
    @State private var minLegInRatio: Double
    @State private var minLegOutRatio: Double
    @State private var swingLookback: Int
    @State private var trendCandleCount: Int
    @State private var minSwingPoints: Int
    @State private var isSaving = false

    init(config: PairConfig, viewModel: ConfigViewModel) {
        self.config = config
        self.viewModel = viewModel
        _enabled = State(initialValue: config.Enabled)
        _emailEnabled = State(initialValue: config.EmailEnabled)
        _trendGranularity = State(initialValue: config.TrendGranularity)
        _minBaseLength = State(initialValue: config.MinBaseLength ?? 1)
        _maxBaseLength = State(initialValue: config.MaxBaseLength ?? 6)
        _minLegInRatio = State(initialValue: config.MinLegInToBaseRangeRatio ?? 1.0)
        _minLegOutRatio = State(initialValue: config.MinLegOutToBaseRangeRatio ?? 1.0)
        _swingLookback = State(initialValue: config.SwingLookback ?? 3)
        _trendCandleCount = State(initialValue: config.TrendCandleCount ?? 60)
        _minSwingPoints = State(initialValue: config.MinSwingPoints ?? 2)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("General") {
                    Toggle("Processing Enabled", isOn: $enabled)
                    Toggle("Email Alerts", isOn: $emailEnabled)
                    Picker("Trend Timeframe", selection: $trendGranularity) {
                        ForEach(["M30", "H1", "H4", "H8", "D", "W"], id: \.self) { tf in
                            Text(tf).tag(tf)
                        }
                    }
                }

                Section("Zone Detection") {
                    Stepper("Min Base Length: \(minBaseLength)", value: $minBaseLength, in: 1...10)
                    Stepper("Max Base Length: \(maxBaseLength)", value: $maxBaseLength, in: 1...20)
                    HStack {
                        Text("Min Leg-In Ratio")
                        Spacer()
                        TextField("", value: $minLegInRatio, format: .number)
                            .keyboardType(.decimalPad)
                            .frame(width: 60)
                            .multilineTextAlignment(.trailing)
                    }
                    HStack {
                        Text("Min Leg-Out Ratio")
                        Spacer()
                        TextField("", value: $minLegOutRatio, format: .number)
                            .keyboardType(.decimalPad)
                            .frame(width: 60)
                            .multilineTextAlignment(.trailing)
                    }
                }

                Section("Trend Detection") {
                    Stepper("Swing Lookback: \(swingLookback)", value: $swingLookback, in: 1...10)
                    Stepper("Candle Count: \(trendCandleCount)", value: $trendCandleCount, in: 10...200, step: 10)
                    Stepper("Min Swing Points: \(minSwingPoints)", value: $minSwingPoints, in: 1...5)
                }

                if let error = viewModel.error {
                    Section {
                        Text(error).foregroundStyle(.red).font(.caption)
                    }
                }
            }
            .navigationTitle("\(config.Instrument.replacingOccurrences(of: "_", with: "/")) \(config.ZoneGranularity)")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task { await save() }
                    }
                    .disabled(isSaving)
                }
            }
        }
    }

    private func save() async {
        isSaving = true
        await viewModel.updateConfig(
            instrument: config.Instrument,
            granularity: config.ZoneGranularity,
            trendGranularity: trendGranularity,
            enabled: enabled, emailEnabled: emailEnabled,
            minBaseLength: minBaseLength, maxBaseLength: maxBaseLength,
            minLegInRatio: minLegInRatio, minLegOutRatio: minLegOutRatio,
            swingLookback: swingLookback, trendCandleCount: trendCandleCount,
            minSwingPoints: minSwingPoints,
            settings: settings, authService: authService
        )
        isSaving = false
        if viewModel.error == nil {
            dismiss()
        }
    }
}

// MARK: - Add New Config

struct AddPairConfigView: View {
    @ObservedObject var viewModel: ConfigViewModel
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @Environment(\.dismiss) var dismiss

    static let instruments = [
        "EUR_USD", "GBP_USD", "USD_JPY", "AUD_USD",
        "NZD_USD", "USD_CAD", "USD_CHF"
    ]

    static let timeframes: [(zone: String, trend: String)] = [
        ("M5", "M30"), ("M15", "H1"), ("M30", "H4"),
        ("H1", "H8"), ("H4", "D"), ("D", "W")
    ]

    @State private var selectedInstrument = "EUR_USD"
    @State private var selectedTFIndex = 3 // H1 default
    @State private var isSaving = false

    var body: some View {
        NavigationStack {
            Form {
                Section("Currency Pair") {
                    Picker("Instrument", selection: $selectedInstrument) {
                        ForEach(Self.instruments, id: \.self) { inst in
                            Text(inst.replacingOccurrences(of: "_", with: "/")).tag(inst)
                        }
                    }
                    .pickerStyle(.wheel)
                    .frame(height: 120)
                }

                Section("Timeframe") {
                    Picker("Zone Timeframe", selection: $selectedTFIndex) {
                        ForEach(0..<Self.timeframes.count, id: \.self) { idx in
                            Text("\(Self.timeframes[idx].zone) (trend: \(Self.timeframes[idx].trend))")
                                .tag(idx)
                        }
                    }
                    .pickerStyle(.wheel)
                    .frame(height: 120)
                }

                if let error = viewModel.error {
                    Section {
                        Text(error).foregroundStyle(.red).font(.caption)
                    }
                }
            }
            .navigationTitle("Add Config")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Add") {
                        Task { await addConfig() }
                    }
                    .disabled(isSaving)
                }
            }
        }
    }

    private func addConfig() async {
        isSaving = true
        let tf = Self.timeframes[selectedTFIndex]

        await viewModel.updateConfig(
            instrument: selectedInstrument,
            granularity: tf.zone,
            trendGranularity: tf.trend,
            enabled: true, emailEnabled: false,
            minBaseLength: 1, maxBaseLength: 6,
            minLegInRatio: 1.0, minLegOutRatio: 1.0,
            swingLookback: 3, trendCandleCount: 60,
            minSwingPoints: 2,
            settings: settings, authService: authService
        )
        isSaving = false
        if viewModel.error == nil {
            dismiss()
        }
    }
}
