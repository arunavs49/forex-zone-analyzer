import SwiftUI

// Thin wrapper so [String: Any] dicts can be used in ForEach
private struct OrderItem: Identifiable {
    let id: String
    let data: [String: Any]
}

/// A sheet that lists all pending limit/stop orders and allows cancellation.
struct PendingOrdersView: View {
    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @Environment(\.dismiss) private var dismiss

    @State private var orders: [OrderItem] = []
    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var cancellingId: String?
    @State private var cancelError: String?

    private let service = ForexDataService()

    var body: some View {
        NavigationStack {
            Group {
                if isLoading {
                    VStack(spacing: 16) {
                        ProgressView()
                        Text("Loading orders…")
                            .foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if let err = errorMessage {
                    VStack(spacing: 12) {
                        Image(systemName: "exclamationmark.triangle")
                            .font(.largeTitle)
                            .foregroundStyle(.red)
                        Text(err)
                            .font(.callout)
                            .multilineTextAlignment(.center)
                            .foregroundStyle(.secondary)
                        Button("Retry") { Task { await load() } }
                            .buttonStyle(.bordered)
                    }
                    .padding()
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if orders.isEmpty {
                    VStack(spacing: 8) {
                        Image(systemName: "list.bullet.clipboard")
                            .font(.system(size: 44))
                            .foregroundStyle(.tertiary)
                        Text("No pending orders")
                            .foregroundStyle(.secondary)
                        if settings.oandaAccountId.isEmpty {
                            Text("Set an account ID in Settings")
                                .font(.caption)
                                .foregroundStyle(.orange)
                        }
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else {
                    List {
                        if let cancelErr = cancelError {
                            Section {
                                Label(cancelErr, systemImage: "xmark.circle.fill")
                                    .foregroundStyle(.red)
                                    .font(.callout)
                            }
                        }
                        Section("\(orders.count) Pending Order\(orders.count == 1 ? "" : "s")") {
                            ForEach(orders) { item in
                                PendingOrderRow(order: item.data, isCancelling: cancellingId == item.id) {
                                    Task { await cancel(orderId: item.id) }
                                }
                            }
                        }
                    }
                }
            }
            .navigationTitle("Pending Orders")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Done") { dismiss() }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        Task { await load() }
                    } label: {
                        Image(systemName: "arrow.clockwise")
                    }
                    .disabled(isLoading)
                }
            }
            .task { await load() }
        }
    }

    private func load() async {
        guard !settings.oandaAccountId.isEmpty else {
            errorMessage = "No account ID set. Configure one in Settings."
            return
        }
        isLoading = true
        errorMessage = nil
        cancelError = nil
        do {
            try await configureService()
            let raw = try await service.fetchPendingOrders(accountId: settings.oandaAccountId)
            orders = raw.compactMap { dict in
                let id: String
                if let s = dict["id"] as? String { id = s }
                else if let n = dict["id"] as? Int { id = String(n) }
                else if let n = dict["id"] as? Double { id = String(Int(n)) }
                else { return nil }
                return OrderItem(id: id, data: dict)
            }
        } catch {
            errorMessage = error.localizedDescription
        }
        isLoading = false
    }

    private func cancel(orderId: String) async {
        cancellingId = orderId
        cancelError = nil
        do {
            try await configureService()
            try await service.cancelOrder(accountId: settings.oandaAccountId, orderId: orderId)
            orders.removeAll { $0.id == orderId }
        } catch {
            cancelError = "Cancel failed: \(error.localizedDescription)"
        }
        cancellingId = nil
    }

    private func configureService() async throws {
        if authService.isSignedIn {
            try await service.configure(url: settings.mcpServerURL, tokenProvider: { await authService.getAccessToken() })
        } else {
            try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
        }
    }
}

// MARK: - Order row

private struct PendingOrderRow: View {
    let order: [String: Any]
    let isCancelling: Bool
    let onCancel: () -> Void

    var body: some View {
        HStack(spacing: 12) {
            // Direction indicator
            VStack(spacing: 2) {
                Image(systemName: isLong ? "arrow.up.circle.fill" : "arrow.down.circle.fill")
                    .font(.title2)
                    .foregroundStyle(isLong ? .green : .red)
                Text(orderType)
                    .font(.system(size: 9, weight: .medium))
                    .foregroundStyle(.secondary)
            }
            .frame(width: 40)

            // Order details
            VStack(alignment: .leading, spacing: 3) {
                HStack {
                    Text(instrument)
                        .font(.callout.weight(.semibold))
                    Spacer()
                    Text("ID: \(orderId)")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundStyle(.secondary)
                }
                HStack(spacing: 12) {
                    labeledValue("Price", value: price)
                    if !stopLoss.isEmpty { labeledValue("SL", value: stopLoss) }
                    if !takeProfit.isEmpty { labeledValue("TP", value: takeProfit) }
                }
                HStack(spacing: 12) {
                    labeledValue("Units", value: units)
                    if !createTime.isEmpty {
                        labeledValue("Created", value: createTime)
                    }
                }
            }

            // Cancel button
            if isCancelling {
                ProgressView().frame(width: 36)
            } else {
                Button(role: .destructive, action: onCancel) {
                    Image(systemName: "trash.circle.fill")
                        .font(.title2)
                        .foregroundStyle(.red.opacity(0.8))
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.vertical, 4)
    }

    @ViewBuilder
    private func labeledValue(_ label: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(label)
                .font(.system(size: 9))
                .foregroundStyle(.secondary)
            Text(value)
                .font(.system(size: 11, design: .monospaced))
        }
    }

    private var orderId: String {
        if let s = order["id"] as? String { return s }
        if let n = order["id"] as? Int { return String(n) }
        return "—"
    }
    private var orderType: String { (order["type"] as? String ?? "ORDER").replacingOccurrences(of: "_ORDER", with: "") }
    private var instrument: String { (order["instrument"] as? String ?? "—").replacingOccurrences(of: "_", with: "/") }
    private var price: String { order["price"] as? String ?? "—" }
    private var units: String {
        if let u = order["units"] as? String { return u }
        if let u = order["units"] as? Int { return "\(u)" }
        return "—"
    }
    private var isLong: Bool {
        if let u = order["units"] as? String { return !u.hasPrefix("-") }
        if let u = order["units"] as? Int { return u > 0 }
        return true
    }
    private var stopLoss: String {
        if let sl = order["stopLossOnFill"] as? [String: Any] { return sl["price"] as? String ?? "" }
        return ""
    }
    private var takeProfit: String {
        if let tp = order["takeProfitOnFill"] as? [String: Any] { return tp["price"] as? String ?? "" }
        return ""
    }
    private var createTime: String {
        guard let raw = order["createTime"] as? String else { return "" }
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let date = formatter.date(from: raw) {
            let rel = RelativeDateTimeFormatter()
            rel.unitsStyle = .abbreviated
            return rel.localizedString(for: date, relativeTo: Date())
        }
        return String(raw.prefix(10))
    }
}
