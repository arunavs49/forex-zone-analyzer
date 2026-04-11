import Foundation

/// MCP client using Streamable HTTP transport (JSON-RPC 2.0 over HTTP POST)
actor MCPClient {
    private let session: URLSession
    private var baseURL: URL
    private var bearerToken: String
    private var requestId: Int = 0
    private var sessionId: String?

    init(baseURL: URL, bearerToken: String) {
        self.baseURL = baseURL
        self.bearerToken = bearerToken
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 60
        config.timeoutIntervalForResource = 120
        self.session = URLSession(configuration: config)
    }

    func updateConfig(baseURL: URL, bearerToken: String) {
        self.baseURL = baseURL
        self.bearerToken = bearerToken
        self.sessionId = nil
    }

    /// Initialize the MCP session
    func initialize() async throws {
        let result: InitializeResult = try await sendRequest(
            method: "initialize",
            params: InitializeParams(
                protocolVersion: "2025-03-26",
                capabilities: ClientCapabilities(),
                clientInfo: ClientInfo(name: "ZoneRadar-iOS", version: "1.0.0")
            )
        )
        _ = result

        // Send initialized notification
        try await sendNotification(method: "notifications/initialized", params: EmptyParams())
    }

    /// Call an MCP tool and return the raw text content
    func callTool(name: String, arguments: [String: Any]) async throws -> String {
        let jsonArgs = try JSONSerialization.data(withJSONObject: arguments)
        let argsDict = try JSONSerialization.jsonObject(with: jsonArgs) as? [String: Any] ?? [:]

        let result: ToolResult = try await sendRequest(
            method: "tools/call",
            params: ToolCallParams(name: name, arguments: argsDict)
        )

        // MCP tool results contain an array of content blocks; concatenate text ones
        return result.content
            .filter { $0.type == "text" }
            .map { $0.text ?? "" }
            .joined()
    }

    // MARK: - JSON-RPC Transport

    private func nextId() -> Int {
        requestId += 1
        return requestId
    }

    private func sendRequest<P: Encodable, R: Decodable>(method: String, params: P) async throws -> R {
        let id = nextId()
        let body = JSONRPCRequest(jsonrpc: "2.0", id: id, method: method, params: params)
        let bodyData = try JSONEncoder().encode(body)

        var request = URLRequest(url: baseURL)
        request.httpMethod = "POST"
        request.httpBody = bodyData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json, text/event-stream", forHTTPHeaderField: "Accept")
        if !bearerToken.isEmpty {
            request.setValue("Bearer \(bearerToken)", forHTTPHeaderField: "Authorization")
        }
        if let sid = sessionId {
            request.setValue(sid, forHTTPHeaderField: "Mcp-Session-Id")
        }

        let (data, response) = try await session.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw MCPError.noResult
        }

        // Capture session ID
        if let sid = httpResponse.value(forHTTPHeaderField: "Mcp-Session-Id") {
            sessionId = sid
        }

        guard (200...299).contains(httpResponse.statusCode) else {
            let body = String(data: data, encoding: .utf8) ?? "No body"
            throw MCPError.httpError(statusCode: httpResponse.statusCode, body: body)
        }

        // Determine the actual response data to decode
        let jsonData: Data
        let contentType = httpResponse.value(forHTTPHeaderField: "Content-Type") ?? ""
        if contentType.contains("text/event-stream") {
            jsonData = extractJSONFromSSE(data)
        } else {
            jsonData = data
        }

        do {
            let rpcResponse = try JSONDecoder().decode(JSONRPCResponse<R>.self, from: jsonData)
            if let error = rpcResponse.error {
                throw MCPError.rpcError(code: error.code, message: error.message)
            }
            guard let result = rpcResponse.result else { throw MCPError.noResult }
            return result
        } catch let decodingError as DecodingError {
            let raw = String(data: jsonData, encoding: .utf8) ?? "(binary)"
            let preview = String(raw.prefix(500))
            throw MCPError.decodingError(detail: "\(decodingError.localizedDescription)\n\nRaw response preview:\n\(preview)")
        }
    }

    private func sendNotification<P: Encodable>(method: String, params: P) async throws {
        let body = JSONRPCNotification(jsonrpc: "2.0", method: method, params: params)
        let bodyData = try JSONEncoder().encode(body)

        var request = URLRequest(url: baseURL)
        request.httpMethod = "POST"
        request.httpBody = bodyData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json, text/event-stream", forHTTPHeaderField: "Accept")
        if !bearerToken.isEmpty {
            request.setValue("Bearer \(bearerToken)", forHTTPHeaderField: "Authorization")
        }
        if let sid = sessionId {
            request.setValue(sid, forHTTPHeaderField: "Mcp-Session-Id")
        }

        let (_, response) = try await session.data(for: request)
        if let httpResponse = response as? HTTPURLResponse,
           let sid = httpResponse.value(forHTTPHeaderField: "Mcp-Session-Id") {
            sessionId = sid
        }
    }

    /// Parse JSON-RPC message from SSE event stream.
    /// SSE format: lines of "event: message\ndata: {json}\n\n"
    /// We want the LAST complete JSON-RPC response (the final message).
    private func extractJSONFromSSE(_ data: Data) -> Data {
        guard let text = String(data: data, encoding: .utf8) else { return data }

        // Split into SSE events (separated by blank lines)
        let events = text.components(separatedBy: "\n\n")

        // Find data lines, take the last one (the final response)
        var lastDataPayload: String?
        for event in events {
            let lines = event.components(separatedBy: "\n")
            let dataLines = lines
                .filter { $0.hasPrefix("data:") || $0.hasPrefix("data: ") }
                .map { line -> String in
                    var l = line
                    l.removeFirst(5) // remove "data:"
                    if l.hasPrefix(" ") { l.removeFirst() } // remove optional space
                    return l
                }
            let payload = dataLines.joined()
            if !payload.isEmpty {
                lastDataPayload = payload
            }
        }

        if let payload = lastDataPayload {
            return Data(payload.utf8)
        }
        return data
    }
}

// MARK: - MCP Protocol Types

enum MCPError: LocalizedError {
    case httpError(statusCode: Int, body: String)
    case rpcError(code: Int, message: String)
    case noResult
    case invalidURL
    case decodingError(detail: String)

    var errorDescription: String? {
        switch self {
        case .httpError(let code, let body): return "HTTP \(code): \(body)"
        case .rpcError(_, let msg): return "MCP Error: \(msg)"
        case .noResult: return "No result returned from MCP server"
        case .invalidURL: return "Invalid MCP server URL"
        case .decodingError(let detail): return "Decoding error: \(detail)"
        }
    }
}

private struct JSONRPCRequest<P: Encodable>: Encodable {
    let jsonrpc: String
    let id: Int
    let method: String
    let params: P
}

private struct JSONRPCNotification<P: Encodable>: Encodable {
    let jsonrpc: String
    let method: String
    let params: P
}

private struct JSONRPCResponse<R: Decodable>: Decodable {
    let jsonrpc: String
    let id: Int?
    let result: R?
    let error: JSONRPCError?
}

private struct JSONRPCError: Decodable {
    let code: Int
    let message: String
}

// MCP initialize types
private struct InitializeParams: Encodable {
    let protocolVersion: String
    let capabilities: ClientCapabilities
    let clientInfo: ClientInfo
}

private struct ClientCapabilities: Encodable {}
private struct ClientInfo: Encodable {
    let name: String
    let version: String
}

struct InitializeResult: Decodable {
    let protocolVersion: String?
    let serverInfo: ServerInfo?
    let capabilities: ServerCapabilities?
}

struct ServerInfo: Decodable {
    let name: String?
    let version: String?
}

struct ServerCapabilities: Decodable {}

// MCP tool call types
private struct ToolCallParams: Encodable {
    let name: String
    let arguments: [String: Any]

    enum CodingKeys: String, CodingKey {
        case name, arguments
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        let jsonData = try JSONSerialization.data(withJSONObject: arguments)
        let jsonObject = try JSONDecoder().decode(AnyCodable.self, from: jsonData)
        try container.encode(jsonObject, forKey: .arguments)
    }
}

struct ToolResult: Decodable {
    let content: [ToolContent]
}

struct ToolContent: Decodable {
    let type: String
    let text: String?
}

private struct EmptyParams: Encodable {}

/// Helper for encoding arbitrary JSON
private struct AnyCodable: Codable {
    let value: Any

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let dict = try? container.decode([String: AnyCodable].self) {
            value = dict.mapValues { $0.value }
        } else if let arr = try? container.decode([AnyCodable].self) {
            value = arr.map { $0.value }
        } else if let str = try? container.decode(String.self) {
            value = str
        } else if let num = try? container.decode(Double.self) {
            value = num
        } else if let bool = try? container.decode(Bool.self) {
            value = bool
        } else if container.decodeNil() {
            value = NSNull()
        } else {
            value = NSNull()
        }
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch value {
        case let dict as [String: Any]:
            let codable = dict.mapValues { AnyCodable(value: $0) }
            try container.encode(codable)
        case let arr as [Any]:
            let codable = arr.map { AnyCodable(value: $0) }
            try container.encode(codable)
        case let str as String:
            try container.encode(str)
        case let num as Double:
            try container.encode(num)
        case let num as Int:
            try container.encode(num)
        case let bool as Bool:
            try container.encode(bool)
        default:
            try container.encodeNil()
        }
    }

    init(value: Any) {
        self.value = value
    }
}
