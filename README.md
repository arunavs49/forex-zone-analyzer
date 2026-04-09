# forex-zone-analyzer

Supply/demand zone detection engine for forex trading, built on the OANDA V20 API.

## What It Does

Detects institutional supply and demand zones from candlestick patterns using a state machine that identifies Rally-Base-Drop (supply) and Drop-Base-Rally (demand) formations.

### Zone Properties

| Property | Description |
|----------|-------------|
| **Type** | Supply or Demand |
| **Base Range** | High/Low price boundaries of the consolidation zone |
| **Freshness** | `Untested` (price never returned), `Tested` (wick entered zone), `Broken` (wick pierced through) |
| **Worked** | `null` (untested), `true` (tested & price bounced ≥2× base width), `false` (tested but no significant bounce) |

### Base Identification

Exciting candles whose range overlaps ≥75% with the existing base are absorbed into the base rather than starting a new leg out. This prevents false zone boundaries from candles that are mostly contained within the consolidation range.

## Tech Stack

| Aspect | Details |
|--------|---------|
| Language | C# (.NET Core 3.1) |
| Solution | `src/ZoneAnalyzer.sln` — 7 projects |
| Broker | OANDA V20 REST API |
| Auth | Bearer token |

## Quick Start

```bash
# Build (requires .NET Core 3.1 SDK)
dotnet build src/ZoneAnalyzer.sln

# Run tests (15 tests)
dotnet test src/ZoneAnalyzer.sln

# Run interactive playground
dotnet run --project src/GeriRemenyi.Oanda.V20.Sdk.Playground
```

## Projects

| Project | Purpose |
|---------|---------|
| `GeriRemenyi.Oanda.V20.Client` | Auto-generated OANDA V20 API client (~202 files) |
| `GeriRemenyi.Oanda.V20.Sdk` | High-level SDK wrapper (connection, candle pagination, trades) |
| `ZoneAnalyzer.PatternAnalysis` | Core zone detection: ZoneFinder state machine, TrendManager, candle classification |
| `ZoneAnalyzer.DataProvider` | Instrument wrapper (pass-through, no caching yet) |
| `GeriRemenyi.Oanda.V20.Sdk.Playground` | Interactive console demo |
| `*.Test` | xUnit test projects |

## Documentation

- **[CODEBASE_REFERENCE.md](CODEBASE_REFERENCE.md)** — Comprehensive architecture, algorithms, and API reference

## Origins

Built on top of:
- https://github.com/geriremenyi/oanda-dotnet-client/
- https://github.com/geriremenyi/oanda-dotnet-sdk