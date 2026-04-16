# Forex Zone Analyzer — MCP Server

A remote [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server that exposes the Forex Zone Analyzer's capabilities as AI-callable tools. Hosted on **Azure Container Apps** with OANDA API credentials stored in **Azure Key Vault** and access secured by **Microsoft Entra ID**.

## Available Tools

### Account Tools
| Tool | Description |
|------|-------------|
| `list_accounts` | List all OANDA trading accounts |
| `get_account_summary` | Account balance, P&L, margin, open trade counts |
| `get_account_details` | Full account details including all positions |
| `get_tradeable_instruments` | List available currency pairs for trading |

### Instrument Tools
| Tool | Description |
|------|-------------|
| `get_candles` | Get OHLC candlestick data (last N candles) |
| `get_candles_by_time` | Get OHLC data for a specific date range |
| `get_supply_demand_zones` | Detect supply/demand zones with freshness analysis |
| `get_trend` | Swing-based trend direction (Up/Down/Sideways) |
| `get_stored_zones` | Pre-computed zones + trend from Table Storage (refreshed by the background worker) |

### Trade & Order Tools
| Tool | Description |
|------|-------------|
| `get_open_trades` | List currently open trades |
| `open_trade` | Open a new market order (⚠️ real money) |
| `close_trade` | Close an existing trade (⚠️ real money) |
| `place_limit_order` | Place a limit order at a specific entry price with SL/TP (⚠️ real money) |
| `get_pending_orders` | List pending (unfilled) orders for an account |
| `get_orders` | List all orders, optionally filtered by state |
| `cancel_order` | Cancel a pending order by ID (⚠️ real money) |

---

## Architecture

```
┌─────────────────────────────────────────────┐
│         MCP Client (Copilot, etc.)          │
│  Authenticates via Entra ID managed identity│
└──────────────┬──────────────────────────────┘
               │ HTTPS (Streamable HTTP)
               ▼
┌─────────────────────────────────────────────┐
│     Azure Container Apps                     │
│     ForexZoneAnalyzer.McpServer             │
│  ┌────────────────────────────────────────┐ │
│  │ ASP.NET Core + MCP HTTP Transport      │ │
│  │ • JWT Bearer Auth (Entra ID)           │ │
│  │ • /mcp endpoint (authenticated)        │ │
│  │ • /health endpoint (public)            │ │
│  └─────────────┬──────────────────────────┘ │
│                │                             │
│  ┌─────────────▼──────────────────────────┐ │
│  │ OandaConnectionService                 │ │
│  │ • Retrieves token from Key Vault       │ │
│  │ • Caches OANDA API connection          │ │
│  └─────────────┬──────────────────────────┘ │
└────────────────┼─────────────────────────────┘
                 │
    ┌────────────▼────────┐     ┌──────────────┐
    │  Azure Key Vault    │     │  OANDA V20   │
    │  (oanda-api-token)  │     │  REST API    │
    └─────────────────────┘     └──────────────┘
```

---

## Local Development

### Prerequisites
- .NET 10 SDK
- An OANDA practice/live account with API token

### Run locally
```bash
cd src/ForexZoneAnalyzer.McpServer

# Set the OANDA token for local dev (use user-secrets for safety)
dotnet user-secrets set "Oanda:ApiToken" "<your-oanda-token>"
dotnet user-secrets set "Oanda:ConnectionType" "FxPractice"

dotnet run
```

The MCP server starts on `http://localhost:5000` (or the port shown in console output).

### Connect VS Code (local)
The repo includes `.vscode/mcp.json` pre-configured for local development. Once the server is running, VS Code / GitHub Copilot will auto-discover the MCP tools.

> **Note:** In Development mode (default for `dotnet run`), authentication is **disabled** so the local MCP client connects without tokens. In production (Azure), Entra ID JWT auth is enforced.

### Run tests
```bash
dotnet test src/ZoneAnalyzer.sln
```

---

## Azure Deployment

### Prerequisites
- Azure CLI (`az`) logged in
- Azure subscription (ID: provided at deploy time)
- An OANDA API token
- An Entra ID app registration for the MCP server

### 1. Create the Entra ID App Registration

```bash
# Create app registration
az ad app create --display-name "forex-mcp-server" \
  --sign-in-audience AzureADMyOrg

# Note the appId (client ID) from the output
# Set the application ID URI
az ad app update --id <app-id> --identifier-uris "api://<app-id>"
```

### 2. Deploy Infrastructure

```bash
# Deploy the Bicep template at subscription scope
az deployment sub create \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters \
    oandaApiToken="<your-oanda-token>" \
    entraIdTenantId="<your-tenant-id>" \
    entraIdClientId="<app-registration-client-id>" \
    oandaConnectionType="FxPractice" \
    imageTag="latest"
```

This creates:
- **Resource Group** (`rg-forex-mcp`)
- **Azure Container Registry** (ACR)
- **Azure Key Vault** with the OANDA token stored as a secret
- **Container Apps Environment** with Log Analytics
- **Container App** with managed identity
- **RBAC**: AcrPull + Key Vault Secrets User for the managed identity

### 3. Build and Push Container Image

```bash
# Get the ACR login server from deployment outputs
ACR_SERVER=$(az deployment sub show --name resources-deployment \
  --query properties.outputs.containerRegistryLoginServer.value -o tsv)

# Login to ACR
az acr login --name ${ACR_SERVER%%.*}

# Build and push (from repo root)
docker build -t ${ACR_SERVER}/forex-mcp-server:latest .
docker push ${ACR_SERVER}/forex-mcp-server:latest

# Update the container app to use the new image
az containerapp update \
  --name ca-forex-mcp \
  --resource-group rg-forex-mcp \
  --image ${ACR_SERVER}/forex-mcp-server:latest
```

### 4. Verify Deployment

```bash
# Check health endpoint
FQDN=$(az containerapp show --name ca-forex-mcp --resource-group rg-forex-mcp \
  --query properties.configuration.ingress.fqdn -o tsv)
curl https://${FQDN}/health
```

### 5. Configure VS Code MCP Client for Remote Server

Update `.vscode/mcp.json` — uncomment the remote section and fill in values:

```json
{
  "servers": {
    "forex-zone-analyzer-remote": {
      "type": "http",
      "url": "https://<CONTAINER_APP_FQDN>/mcp",
      "headers": {
        "Authorization": "Bearer ${command:./scripts/get-mcp-token.sh}"
      }
    }
  }
}
```

The included `scripts/get-mcp-token.sh` fetches an Entra ID token via Azure CLI:

```bash
# Set your app registration client ID
export APP_CLIENT_ID="<your-app-registration-client-id>"

# Ensure you're logged in
az login

# Test the token script
./scripts/get-mcp-token.sh
```

---

## Configuration Reference

| Setting | Description | Required |
|---------|-------------|----------|
| `KeyVault:Uri` | Key Vault URI (e.g. `https://kv-forex-mcp-xxx.vault.azure.net/`) | Production |
| `KeyVault:OandaTokenSecretName` | Secret name in Key Vault (default: `oanda-api-token`) | No |
| `Oanda:ConnectionType` | `FxPractice` or `FxTrade` | Yes |
| `Oanda:ApiToken` | Direct token (local dev only, not for production) | Local dev |
| `AzureAd:Instance` | `https://login.microsoftonline.com/` | Production |
| `AzureAd:TenantId` | Entra ID tenant ID | Production |
| `AzureAd:ClientId` | App registration client ID | Production |
| `AzureAd:Audience` | Token audience (e.g. `api://<client-id>`) | Production |

---

## Project Structure

```
src/ForexZoneAnalyzer.McpServer/
├── Program.cs                      # Entry point, DI, auth, MCP setup
├── Services/
│   ├── IOandaConnectionService.cs  # Interface
│   └── OandaConnectionService.cs   # Key Vault + OANDA connection management
├── Tools/
│   ├── AccountTools.cs             # Account MCP tools (4 tools)
│   ├── InstrumentTools.cs          # Candles, zones, trends (4 tools)
│   ├── TradeTools.cs               # Trade/order management (7 tools)
│   └── StoredZoneTools.cs          # Pre-computed zones from Table Storage (1 tool)
├── appsettings.json                # Configuration template
└── ForexZoneAnalyzer.McpServer.csproj

src/ForexZoneAnalyzer.McpServer.Test/
├── AccountToolsTests.cs
├── InstrumentToolsTests.cs
├── TradeToolsTests.cs
├── StoredZoneToolsTests.cs
└── OandaConnectionServiceTests.cs

infra/
├── main.bicep                      # Subscription-scoped deployment
├── main.bicepparam                 # Parameter file template
└── modules/
    └── resources.bicep             # All Azure resources
```

## Security Considerations

- **Never commit API tokens** — OANDA tokens are stored exclusively in Azure Key Vault
- **Managed identity** — Container App uses user-assigned managed identity for Key Vault access (no credentials to manage)
- **Entra ID auth** — MCP endpoint requires JWT Bearer tokens from your Azure AD tenant
- **RBAC** — Least-privilege roles: AcrPull for container registry, Key Vault Secrets User for secrets
- **Trade tools warning** — `open_trade`, `close_trade`, `place_limit_order`, and `cancel_order` execute real trades/orders; consider restricting access or using FxPractice
