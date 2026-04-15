import SwiftUI

/// Confirmation sheet for placing a zone-derived limit order.
struct PlaceOrderSheet: View {
    let zone: Zone
    let instrument: Instrument

    @EnvironmentObject var settings: AppSettings
    @EnvironmentObject var authService: AuthService
    @Environment(\.dismiss) private var dismiss

    @State private var orderParams: ZoneOrderParameters?
    @State private var isPlacing = false
    @State private var resultMessage: String?
    @State private var isError = false

    private let service = ForexDataService()

    var body: some View {
        NavigationStack {
            Group {
                if let params = orderParams {
                    orderForm(params: params)
                } else {
                    ProgressView("Calculating…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            .navigationTitle("Place Zone Order")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") { dismiss() }
                        .disabled(isPlacing)
                }
            }
            .onAppear {
                orderParams = zone.orderParameters(
                    instrumentSymbol: instrument.rawValue,
                    riskAmountUSD: settings.riskAmountUSD
                )
            }
        }
    }

    @ViewBuilder
    private func orderForm(params: ZoneOrderParameters) -> some View {
        Form {
            Section("Zone") {
                LabeledContent("Instrument", value: instrument.displayName)
                LabeledContent("Type", value: zone.type.rawValue)
                    .foregroundStyle(zone.type == .Supply ? .red : .green)
                LabeledContent("Freshness", value: zone.freshness.rawValue)
                LabeledContent("Base Range") {
                    Text("\(formatPrice(zone.baseRangeLow)) — \(formatPrice(zone.baseRangeHigh))")
                        .font(.system(.body, design: .monospaced))
                }
            }

            Section("Order") {
                LabeledContent("Direction") {
                    Text(params.direction)
                        .foregroundStyle(params.direction == "Long" ? .green : .red)
                        .fontWeight(.semibold)
                }
                LabeledContent("Type", value: "Limit")
                LabeledContent("Entry") {
                    Text(formatPrice(params.entryPrice))
                        .font(.system(.body, design: .monospaced))
                }
            }

            Section("Risk Management") {
                LabeledContent("Stop Loss") {
                    VStack(alignment: .trailing, spacing: 2) {
                        Text(formatPrice(params.stopLossPrice))
                            .font(.system(.body, design: .monospaced))
                        Text("\(params.stopLossPips) pips")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                LabeledContent("Take Profit") {
                    VStack(alignment: .trailing, spacing: 2) {
                        Text(formatPrice(params.takeProfitPrice))
                            .font(.system(.body, design: .monospaced))
                        Text("\(params.takeProfitPips) pips · 2:1 R:R")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                LabeledContent("Position Size") {
                    VStack(alignment: .trailing, spacing: 2) {
                        Text("\(params.units.formatted()) units")
                            .font(.system(.body, design: .monospaced))
                        Text("~$\(Int(params.riskAmountUSD)) risk (approx.)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }

            Section("Account") {
                if settings.oandaAccountId.isEmpty {
                    Label("Set Account ID in Settings before placing orders", systemImage: "exclamationmark.triangle.fill")
                        .foregroundStyle(.orange)
                        .font(.callout)
                } else {
                    LabeledContent("Account", value: settings.oandaAccountId)
                        .font(.system(.body, design: .monospaced))
                }
            }

            if let msg = resultMessage {
                Section {
                    HStack(spacing: 8) {
                        Image(systemName: isError ? "xmark.circle.fill" : "checkmark.circle.fill")
                            .foregroundStyle(isError ? .red : .green)
                        Text(msg)
                            .font(.callout)
                            .foregroundStyle(isError ? .red : .primary)
                    }
                }
            }

            Section {
                Button {
                    Task { await placeOrder(params: params) }
                } label: {
                    HStack {
                        Spacer()
                        if isPlacing {
                            ProgressView()
                                .tint(.white)
                        } else {
                            Label(
                                "Place \(params.direction) Limit Order",
                                systemImage: params.direction == "Long" ? "arrow.up.circle.fill" : "arrow.down.circle.fill"
                            )
                            .fontWeight(.semibold)
                        }
                        Spacer()
                    }
                }
                .listRowBackground(
                    RoundedRectangle(cornerRadius: 10)
                        .fill(params.direction == "Long" ? Color.green : Color.red)
                )
                .foregroundStyle(.white)
                .disabled(isPlacing || settings.oandaAccountId.isEmpty)
            }
        }
    }

    private func placeOrder(params: ZoneOrderParameters) async {
        isPlacing = true
        resultMessage = nil
        isError = false

        do {
            if authService.isSignedIn {
                try await service.configure(url: settings.mcpServerURL, tokenProvider: { await authService.getAccessToken() })
            } else {
                try await service.configure(url: settings.mcpServerURL, token: settings.bearerToken)
            }

            let response = try await service.placeLimitOrder(
                accountId: settings.oandaAccountId,
                instrument: instrument.rawValue,
                params: params
            )

            // Parse order ID from response JSON if present
            if let data = response.data(using: .utf8),
               let dict = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let orderFill = dict["orderCreateTransaction"] as? [String: Any],
               let orderId = orderFill["id"] {
                resultMessage = "Order placed — ID: \(orderId)"
            } else {
                resultMessage = "Order placed successfully"
            }
            isError = false

        } catch {
            resultMessage = error.localizedDescription
            isError = true
        }

        isPlacing = false
    }

    private func formatPrice(_ price: Double) -> String {
        price < 10
            ? String(format: "%.5f", price)
            : String(format: "%.3f", price)
    }
}
