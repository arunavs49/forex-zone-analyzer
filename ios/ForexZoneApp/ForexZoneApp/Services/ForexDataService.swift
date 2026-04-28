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

    /// Fetch pending (unfilled) orders for an account
    func fetchPendingOrders(accountId: String) async throws -> [[String: Any]] {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_pending_orders",
            arguments: ["accountId": accountId]
        )

        if let data = json.data(using: .utf8),
           let array = try? JSONSerialization.jsonObject(with: data) as? [[String: Any]] {
            return array
        }
        return []
    }

    /// Cancel a pending order by ID
    func cancelOrder(accountId: String, orderId: String) async throws {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        _ = try await client.callTool(
            name: "cancel_order",
            arguments: ["accountId": accountId, "orderId": orderId]
        )
    }

    // MARK: - Pair Config Management

    /// List all pair+TF configurations with status
    func listPairConfigs() async throws -> PairConfigListResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(name: "list_pair_configs", arguments: [:] as [String: String])
        let data = Data(json.utf8)
        return try JSONDecoder().decode(PairConfigListResponse.self, from: data)
    }

    /// Get config + status for a specific pair+TF
    func getPairConfig(instrument: String, granularity: String) async throws -> PairConfig {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_pair_config",
            arguments: ["instrument": instrument, "granularity": granularity]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(PairConfig.self, from: data)
    }

    /// Create or update a pair+TF configuration
    func updatePairConfig(
        instrument: String, granularity: String, trendGranularity: String,
        enabled: Bool, emailEnabled: Bool,
        minBaseLength: Int, maxBaseLength: Int,
        minLegInRatio: Double, minLegOutRatio: Double,
        swingLookback: Int, trendCandleCount: Int, minSwingPoints: Int
    ) async throws -> PairConfigUpdateResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "update_pair_config",
            arguments: [
                "instrument": instrument,
                "granularity": granularity,
                "trendGranularity": trendGranularity,
                "enabled": enabled,
                "emailEnabled": emailEnabled,
                "minBaseLength": minBaseLength,
                "maxBaseLength": maxBaseLength,
                "minLegInToBaseRangeRatio": minLegInRatio,
                "minLegOutToBaseRangeRatio": minLegOutRatio,
                "swingLookback": swingLookback,
                "trendCandleCount": trendCandleCount,
                "minSwingPoints": minSwingPoints
            ]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(PairConfigUpdateResponse.self, from: data)
    }

    /// Toggle processing enabled/disabled
    func setPairEnabled(instrument: String, granularity: String, enabled: Bool) async throws -> PairConfigUpdateResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "set_pair_enabled",
            arguments: ["instrument": instrument, "granularity": granularity, "enabled": enabled]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(PairConfigUpdateResponse.self, from: data)
    }

    /// Toggle email alerts
    func setPairEmailEnabled(instrument: String, granularity: String, emailEnabled: Bool) async throws -> PairConfigUpdateResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "set_pair_email_enabled",
            arguments: ["instrument": instrument, "granularity": granularity, "emailEnabled": emailEnabled]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(PairConfigUpdateResponse.self, from: data)
    }

    /// Get processing status for a pair+TF
    func getPairStatus(instrument: String, granularity: String) async throws -> PairStatusResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_pair_status",
            arguments: ["instrument": instrument, "granularity": granularity]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(PairStatusResponse.self, from: data)
    }

    // MARK: - Strategy Optimization

    /// Start a strategy optimization run
    func startStrategyRun(instrument: String, granularity: String, lookbackMonths: Int = 6) async throws -> StartRunResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "start_strategy_run",
            arguments: ["instrument": instrument, "granularity": granularity, "lookbackMonths": lookbackMonths]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(StartRunResponse.self, from: data)
    }

    /// Get status and results of a strategy run
    func getStrategyRun(instrument: String, granularity: String, runId: String) async throws -> StrategyRun {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "get_strategy_run",
            arguments: ["instrument": instrument, "granularity": granularity, "runId": runId]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(StrategyRun.self, from: data)
    }

    /// List all strategy runs for a pair+TF
    func listStrategyRuns(instrument: String, granularity: String) async throws -> StrategyRunListResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "list_strategy_runs",
            arguments: ["instrument": instrument, "granularity": granularity]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(StrategyRunListResponse.self, from: data)
    }

    /// List recent strategy runs across all pairs
    func listRecentStrategyRuns(limit: Int = 10) async throws -> RecentStrategyRunListResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "list_recent_strategy_runs",
            arguments: ["limit": limit]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(RecentStrategyRunListResponse.self, from: data)
    }

    /// Apply best config from a strategy run
    func applyStrategyResult(instrument: String, granularity: String, runId: String) async throws -> PairConfigUpdateResponse {
        try await ensureInitialized()
        guard let client = client else { throw MCPError.invalidURL }

        let json = try await client.callTool(
            name: "apply_strategy_result",
            arguments: ["instrument": instrument, "granularity": granularity, "runId": runId]
        )
        let data = Data(json.utf8)
        return try JSONDecoder().decode(PairConfigUpdateResponse.self, from: data)
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
