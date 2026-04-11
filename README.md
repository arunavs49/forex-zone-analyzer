# forex-zone-analyzer

Supply/demand zone detection engine for forex trading, built on the OANDA V20 API.

## What It Does

Detects institutional supply and demand zones from candlestick patterns using a state machine that identifies Rally-Base-Drop (supply) and Drop-Base-Rally (demand) formations. Runs as both an MCP server for interactive analysis and a background worker for real-time zone monitoring with email alerts.

### Zone Properties

| Property | Description |
|----------|-------------|
| **Type** | Supply or Demand |
| **Base Range** | High/Low price boundaries of the consolidation zone |
| **Freshness** | `Untested` (price never returned), `Tested` (wick entered zone), `Broken` (wick pierced through) |
| **Worked** | `null` (untested), `true` (tested & price bounced ≥2× base width), `false` (tested but no significant bounce) |
| **SubZone** | `true` if zone overlaps ≥10% with a prior same-type unbroken zone |
| **Quarters Theory** | Whether the zone base aligns with institutional quarter levels |

### Base Identification

Exciting candles whose range overlaps ≥75% with the existing base are absorbed into the base rather than starting a new leg out. This prevents false zone boundaries from candles that are mostly contained within the consolidation range.

### Zone Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| MinBaseLength | 1 | Minimum candles in base |
| MaxBaseLength | 6 | Maximum candles in base |
| LegInToBaseRatio | 1.0 | Min leg-in to base size ratio |
| LegOutToBaseRatio | 1.0 | Min leg-out to base size ratio |

## Tech Stack

| Aspect | Details |
|--------|---------|
| Language | C# (.NET 10) |
| Solution | `src/ZoneAnalyzer.sln` — 11 projects |
| Broker | OANDA V20 REST API |
| Auth | Bearer token (OANDA), Entra ID (MCP server) |
| Hosting | Azure Container Apps |
| Infra | Bicep (IaC) |
| CI/CD | GitHub Actions (OIDC) |
| Email | Azure Communication Services |
| Storage | Azure Table Storage (zone persistence) |

## Quick Start

```bash
# Build (requires .NET 10 SDK)
dotnet build src/ZoneAnalyzer.sln

# Run tests (1320 tests)
dotnet test src/ZoneAnalyzer.sln

# Run interactive playground
dotnet run --project src/GeriRemenyi.Oanda.V20.Sdk.Playground

# Run MCP server locally
dotnet run --project src/ForexZoneAnalyzer.McpServer

# Run worker locally (Development mode — console notifications)
dotnet run --project src/ForexZoneAnalyzer.Worker
```

## Projects

| Project | Purpose |
|---------|---------|
| `GeriRemenyi.Oanda.V20.Client` | Auto-generated OANDA V20 API client (~202 files) |
| `GeriRemenyi.Oanda.V20.Sdk` | High-level SDK wrapper (connection, candle pagination, trades) |
| `ZoneAnalyzer.PatternAnalysis` | Core zone detection: ZoneFinder state machine, swing-based TrendManager, candle classification |
| `ZoneAnalyzer.DataProvider` | Instrument wrapper (pass-through) |
| `ForexZoneAnalyzer.McpServer` | MCP server with 11 tools for interactive zone analysis |
| `ForexZoneAnalyzer.Worker` | Background worker monitoring currency pairs for new zones with email alerts |
| `GeriRemenyi.Oanda.V20.Sdk.Playground` | Interactive console demo |
| `*.Test` | xUnit test projects (1372 tests total) |

## Azure Infrastructure

Deployed via Bicep (`infra/`) to Azure Container Apps:

| Resource | Purpose |
|----------|---------|
| Container App (`ca-forex-mcp`) | MCP server with Entra ID auth |
| Container App (`ca-forex-mcp-worker`) | Zone monitoring worker (no ingress) |
| Container Registry | Docker images for both apps |
| Key Vault | OANDA API token storage |
| Storage Account | Zone persistence (Table Storage) |
| Communication Services | Email notifications for new zones |
| Managed Identity | Passwordless access to all services |
| Log Analytics | Container Apps logging |

## CI/CD

GitHub Actions pipeline (`.github/workflows/deploy.yml`):
1. **Build & Test** — restore, build, run all 1320 tests
2. **Deploy** — build Docker images, deploy Bicep infra, update Container Apps

Triggered on every push to `main`. Uses OIDC federated credentials (no stored secrets for Azure auth).

## Worker Service

Monitors configurable currency pairs (default: EUR_USD, GBP_USD, USD_JPY, AUD_USD) on 15-minute timeframe for new supply/demand zones.

Features:
- **Incremental candle caching** — 2000-candle sliding window, fetches only new candles after initial load
- **Zone persistence** — Azure Table Storage (prod) / in-memory (dev)
- **Change detection** — only alerts on genuinely new zones
- **Email notifications** — Azure Communication Services (prod) / console (dev)
- **Trend context** — swing-based trend detection (Higher Highs/Higher Lows) included in alerts
- **Configurable** — instruments, timeframes, poll interval, zone configuration all via appsettings

## Documentation

- **[CODEBASE_REFERENCE.md](CODEBASE_REFERENCE.md)** — Comprehensive architecture, algorithms, and API reference

## Origins

Built on top of:
- https://github.com/geriremenyi/oanda-dotnet-client/
- https://github.com/geriremenyi/oanda-dotnet-sdk