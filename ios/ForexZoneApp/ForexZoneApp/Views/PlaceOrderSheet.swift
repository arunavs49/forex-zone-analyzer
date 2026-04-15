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
    @State private var showSuccessAlert = false
    @State private var successMessage = ""

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
            .alert("Order Placed", isPresented: $showSuccessAlert) {
                Button("OK") { dismiss() }
            } message: {
                Text(successMessage)
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

            if isError, let msg = resultMessage {
                Section {
                    HStack(spacing: 8) {
                        Image(systemName: "xmark.circle.fill")
                            .foregroundStyle(.red)
                        Text(msg)
                            .font(.callout)
                            .foregroundStyle(.red)
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
            var orderId: String = ""
            if let data = response.data(using: .utf8),
               let dict = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let orderFill = dict["orderCreateTransaction"] as? [String: Any],
               let id = orderFill["id"] {
                orderId = "\(id)"
            }

            isError = false
            isPlacing = false

            // Build success message and show alert (dismiss happens on OK)
            let pair = instrument.rawValue.replacingOccurrences(of: "_", with: "/")
            let orderLine = orderId.isEmpty ? "" : "\nOrder ID: \(orderId)"
            successMessage = "\(params.direction) \(pair) limit order submitted.\(orderLine)\n\nEntry: \(formatPrice(params.entryPrice))  ·  SL: \(params.stopLossPips)p  ·  TP: \(params.takeProfitPips)p\n\(params.units.formatted()) units  ·  ~$\(Int(params.riskAmountUSD)) risk"
            showSuccessAlert = true

        } catch {
            resultMessage = error.localizedDescription
            isError = true
            isPlacing = false
        }
    }

    private func formatPrice(_ price: Double) -> String {
        price < 10
            ? String(format: "%.5f", price)
            : String(format: "%.3f", price)
    }
}
