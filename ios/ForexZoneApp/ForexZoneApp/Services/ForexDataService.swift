import Foundation

/// Service that wraps MCP tool calls into typed Swift methods
class ForexDataService: ObservableObject {
    private var client: MCPClient?
    private var isInitialized = false

    func configure(url: String, token: String) async throws {
        guard let baseURL = URL(string: url) else {
            throw MCPError.invalidURL
        }

        if let existing = client {
            await existing.updateConfig(baseURL: baseURL, bearerToken: token)
        } else {
            client = MCPClient(baseURL: baseURL, bearerToken: token)
        }

        isInitialized = false
    }

    /// Configure with an async token provider (e.g. MSAL) for automatic refresh
    func configure(url: String, tokenProvider: @escaping @Sendable () async -> String?) async throws {
        guard let baseURL = URL(string: url) else {
            throw MCPError.invalidURL
        }

        if let existing = client {
            await existing.updateConfig(baseURL: baseURL, tokenProvider: tokenProvider)
        } else {
            client = MCPClient(baseURL: baseURL, bearerToken: "")
            await client?.updateConfig(baseURL: baseURL, tokenProvider: tokenProvider)
        }

        isInitialized = false
    }

    private func ensureInitialized() async throws {
        guard let client = client else {
            throw MCPError.invalidURL
        }
        if !isInitialized {
            try await client.initialize()
            isInitialized = true
        }
    }

    /// Fetch candlestick data for an instrument
    func getCandles(instrument: String, granularity: String, count: Int = 500) async throws -> [Candle] {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_candles",
            arguments: [
                "instrument": instrument,
                "granularity": granularity,
                "count": count
            ]
        )

        let data = Data(json.utf8)
        do {
            return try JSONDecoder().decode([Candle].self, from: data)
        } catch let DecodingError.typeMismatch(type, context) {
            let path = context.codingPath.map(\.stringValue).joined(separator: ".")
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "Candle type mismatch: expected \(type) at \(path)\n\(context.debugDescription)\n\nResponse preview:\n\(preview)")
        } catch let DecodingError.keyNotFound(key, context) {
            let path = context.codingPath.map(\.stringValue).joined(separator: ".")
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "Candle missing key '\(key.stringValue)' at \(path)\n\(context.debugDescription)\n\nResponse preview:\n\(preview)")
        } catch let DecodingError.valueNotFound(type, context) {
            let path = context.codingPath.map(\.stringValue).joined(separator: ".")
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "Candle null value for \(type) at \(path)\n\(context.debugDescription)\n\nResponse preview:\n\(preview)")
        } catch {
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "Candle decode failed: \(error)\n\nResponse preview:\n\(preview)")
        }
    }

    /// Fetch supply and demand zones
    func getZones(instrument: String, granularity: String, count: Int = 500) async throws -> ZoneAnalysisResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_supply_demand_zones",
            arguments: [
                "instrument": instrument,
                "granularity": granularity,
                "count": count
            ]
        )

        let data = Data(json.utf8)
        do {
            return try JSONDecoder().decode(ZoneAnalysisResponse.self, from: data)
        } catch let DecodingError.typeMismatch(type, context) {
            let path = context.codingPath.map(\.stringValue).joined(separator: ".")
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "Zone type mismatch: expected \(type) at \(path)\n\(context.debugDescription)\n\nResponse preview:\n\(preview)")
        } catch let DecodingError.keyNotFound(key, context) {
            let path = context.codingPath.map(\.stringValue).joined(separator: ".")
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "Zone missing key '\(key.stringValue)' at \(path)\n\(context.debugDescription)\n\nResponse preview:\n\(preview)")
        } catch {
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "Zone decode failed: \(error)\n\nResponse preview:\n\(preview)")
        }
    }

    /// Fetch pre-computed zones and trend from storage
    func getStoredZones(instrument: String, granularity: String) async throws -> ZoneAnalysisResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_stored_zones",
            arguments: [
                "instrument": instrument,
                "granularity": granularity
            ]
        )

        let data = Data(json.utf8)
        do {
            return try JSONDecoder().decode(ZoneAnalysisResponse.self, from: data)
        } catch let DecodingError.typeMismatch(type, context) {
            let path = context.codingPath.map(\.stringValue).joined(separator: ".")
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "StoredZone type mismatch: expected \(type) at \(path)\n\(context.debugDescription)\n\nResponse preview:\n\(preview)")
        } catch let DecodingError.keyNotFound(key, context) {
            let path = context.codingPath.map(\.stringValue).joined(separator: ".")
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "StoredZone missing key '\(key.stringValue)' at \(path)\n\(context.debugDescription)\n\nResponse preview:\n\(preview)")
        } catch {
            let preview = String(json.prefix(500))
            throw MCPError.decodingError(detail: "StoredZone decode failed: \(error)\n\nResponse preview:\n\(preview)")
        }
    }

    /// Fetch trend direction
    func getTrend(instrument: String, granularity: String) async throws -> String {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_trend",
            arguments: [
                "instrument": instrument,
                "granularity": granularity,
                "count": 100
            ]
        )

        // Parse the trend from JSON response
        if let data = json.data(using: .utf8),
           let dict = try JSONSerialization.jsonObject(with: data) as? [String: Any],
           let trend = dict["Trend"] as? String {
            return trend
        }
        return "Unknown"
    }

    /// Fetch available OANDA account IDs
    func fetchAccounts() async throws -> [String] {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(name: "list_accounts", arguments: [:] as [String: String])

        if let data = json.data(using: .utf8),
           let array = try? JSONSerialization.jsonObject(with: data) as? [[String: Any]] {
            return array.compactMap { $0["Id"] as? String }
        }
        return []
    }

    /// Place a limit order derived from a zone's parameters
    func placeLimitOrder(
        accountId: String,
        instrument: String,
        params: ZoneOrderParameters
    ) async throws -> String {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        return try await client.callTool(
            name: "place_limit_order",
            arguments: [
                "accountId": accountId,
                "instrument": instrument,
                "direction": params.direction,
                "entryPrice": params.entryPrice,
                "stopLossPips": params.stopLossPips,
                "takeProfitPips": params.takeProfitPips,
                "units": params.units
            ]
        )
    }
}
