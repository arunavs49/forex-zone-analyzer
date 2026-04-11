# forex-zone-analyzer — Comprehensive Codebase Documentation

> **Purpose:** Reference document for building a new project that reuses this codebase's
> trade setup, notification, and backtesting capabilities.

---

## 1. High-Level Overview

| Aspect        | Details |
|---------------|---------|
| **Language**  | C# (.NET 10) |
| **Solution**  | `src/ZoneAnalyzer.sln` — 11 projects |
| **Broker**    | OANDA V20 REST API |
| **Auth**      | Bearer token (OANDA), Entra ID (MCP server) |
| **Hosting**   | Azure Container Apps |
| **Infra**     | Bicep (IaC) |
| **CI/CD**     | GitHub Actions (OIDC federated credentials) |
| **Core Idea** | Detect supply/demand zones from candlestick patterns and trend direction to identify trade setups |

### Architecture Diagram

```
┌──────────────────────────────────────────────────────────────┐
│               ForexZoneAnalyzer.McpServer                    │
│         MCP server: 11 tools for zone analysis               │
│         (Azure Container Apps + Entra ID auth)               │
├──────────────────────────────────────────────────────────────┤
│               ForexZoneAnalyzer.Worker                        │
│         Background zone monitor + email alerts                │
│         (Azure Container Apps, no ingress)                    │
│         • ZoneMonitorService (polling loop)                   │
│         • CandleCacheService (2000 sliding window)            │
│         • IZoneStore (Table Storage / in-memory)              │
│         • INotificationService (ACS Email / console)          │
├──────────────────────────────────────────────────────────────┤
│               Sdk.Playground (Console App)                    │
│         Interactive menus: accounts, instruments, trades      │
├──────────────────────────┬───────────────────────────────────┤
│  ZoneAnalyzer            │  GeriRemenyi.Oanda.V20.Sdk        │
│  .PatternAnalysis        │  (SDK wrapper)                    │
│  ─────────────────       │  ───────────────                  │
│  • ZoneManager           │  • OandaApiConnection             │
│  • ZoneFinder            │  • Account / IAccount             │
│  • TrendManager (swing)  │  • Instrument / IInstrument       │
│  • TrendConfiguration    │  • Trades / ITrades               │
├──────────────────────────┤  • DateTimeRange / Extensions     │
│  ZoneAnalyzer            ├───────────────────────────────────┤
│  .DataProvider           │  GeriRemenyi.Oanda.V20.Client     │
│  ─────────────────       │  (Auto-generated OpenAPI client)  │
│  • CachedInstrument      │  ───────────────────              │
│    (pass-through wrapper) │  • AccountApi, InstrumentApi     │
│                          │  • OrderApi, TradeApi             │
│                          │  • PositionApi, PricingApi        │
│                          │  • TransactionApi                 │
│                          │  • 180+ model DTOs                │
└──────────────────────────┴───────────────────────────────────┘
                                    │
                                    ▼
                        OANDA V20 REST API
                   (fxpractice / fxtrade servers)

Azure Infrastructure (Bicep):
┌────────────────────────────────────────────────────────┐
│  Container Apps Environment                             │
│  ├── ca-forex-mcp (MCP server, external ingress)       │
│  └── ca-forex-mcp-worker (worker, no ingress)          │
├────────────────────────────────────────────────────────┤
│  ACR │ Key Vault │ Storage Account │ ACS Email │ MI    │
└────────────────────────────────────────────────────────┘
```

---

## 2. Project-by-Project Breakdown

### 2.1 GeriRemenyi.Oanda.V20.Client (Auto-generated API Client)

- **Source:** OpenAPI Generator 3.0.25
- **~202 .cs files** (7 API classes, 15 runtime/client classes, 180 model DTOs)
- **Dependencies:** RestSharp 112.1, Newtonsoft.Json 13.0, JsonSubTypes 2.0

#### API Endpoints Covered

| API Class        | Endpoints |
|------------------|-----------|
| **AccountApi**   | `GET /accounts`, `GET /accounts/{id}`, `GET .../summary`, `GET .../changes`, `GET .../instruments`, `PATCH .../configuration` |
| **InstrumentApi**| `GET /instruments/{name}/candles`, `GET .../orderBook`, `GET .../positionBook` |
| **OrderApi**     | `POST .../orders` (create), `GET .../orders`, `GET .../pendingOrders`, `GET/PUT .../orders/{id}`, `PUT .../cancel`, `PUT .../clientExtensions` |
| **TradeApi**     | `GET .../trades`, `GET .../openTrades`, `GET .../trades/{id}`, `PUT .../close`, `PUT .../orders`, `PUT .../clientExtensions` |
| **PositionApi**  | `GET .../positions`, `GET .../openPositions`, `GET .../positions/{instrument}`, `PUT .../close` |
| **PricingApi**   | `GET .../pricing` |
| **TransactionApi** | `GET .../transactions`, `GET .../transactions/{id}`, `GET .../idrange`, `GET .../sinceid` |

#### Key Models (reusable for new project)

| Model | Description |
|-------|-------------|
| `Account` | Balance, PL, margin, open trade/position/order counts |
| `Trade` | Instrument, units, price, PL, state, dependent orders |
| `MarketOrder` | Market order with all fill/trigger details |
| `TrailingStopLossDetails` | Distance-based trailing SL |
| `Candlestick` | Time, bid/mid/ask OHLC, volume, complete flag |
| `CandlestickData` | Individual OHLC price set |
| `InstrumentName` (enum) | All OANDA forex pairs (EUR_USD, GBP_JPY, etc.) |
| `CandlestickGranularity` (enum) | S5 through M (monthly) timeframes |
| `ZoneType` (enum) | Supply / Demand zone classification |
| `ZoneFreshness` (enum) | Untested / Tested / Broken zone lifecycle |
| `Zone` | Zone model with Type, BaseRange, Freshness, Worked, SubZone, Quarters Theory |

#### Authentication

```csharp
// Bearer token via Configuration.AccessToken
// Sent as: Authorization: Bearer <token>
// Base URLs:
//   Practice: https://api-fxpractice.oanda.com/v3
//   Live:     https://api-fxtrade.oanda.com/v3
```

---

### 2.2 GeriRemenyi.Oanda.V20.Sdk (High-Level Wrapper)

**21 files** — thin but useful abstraction over the generated client.

#### Core Classes

| Class | Role | Key Methods |
|-------|------|-------------|
| `OandaApiConnection` | Main entry point; creates connection, validates credentials, caches account/instrument wrappers | `GetAccount()`, `GetAccounts()`, `GetInstrument()` |
| `OandaApiConnectionFactory` | Static factory | `CreateConnection(type, token)` |
| `Account` | Account operations wrapper | `GetDetails()`, `GetSummary()`, `GetTradeableInstruments()`, `GetChanges()` + async variants |
| `Instrument` | Candle retrieval with auto-pagination | `GetCandlesByTime()`, `GetLastNCandles()` + async variants |
| `Trades` | Trade execution | `GetOpenTrades()`, `OpenTrade()` + async variants |

#### Smart Candle Pagination (important for backtesting)

OANDA limits candle requests to **5000 candles**. The SDK automatically:
1. Checks if the requested time range exceeds 5000 candles for the given granularity
2. Splits the range into multiple sub-requests using `GranularityExtensions.ExplodeToMultipleCandleRanges()`
3. Merges results seamlessly

```csharp
// GranularityExtensions methods:
AreMultipleQueriesRequired(granularity, from, to)
ExplodeToMultipleCandleRanges(granularity, from, to)
GetNumberOfCandlesForTimeRange(granularity, from, to)
GetInSeconds(granularity)  // maps granularity → seconds
```

#### Trade Execution Details

```csharp
// Trades.OpenTrade() creates a MARKET order with trailing stop loss
// Direction: Long => +1 units, Short => -1 units
// Pip conversion: JPY pairs use 0.01, all others use 0.0001
// Order type: MarketOrder with TrailingStopLossDetails
```

#### Enums & Types

| Type | Values |
|------|--------|
| `OandaConnectionType` | `FxPractice`, `FxTrade` |
| `PricingComponent` | `Bid`, `Mid`, `Ask` |
| `TradeDirection` | `Long`, `Short` |
| `DateTimeRange` | Simple `From`/`To` struct |

#### Exceptions

- `ConnectionInitializationException` — thrown when initial account fetch fails
- `NoSuchAccountException` — thrown when requested account ID not found

---

### 2.3 ZoneAnalyzer.DataProvider

**Minimal project — 1 class: `CachedInstrument`**

- Despite the name, **no actual caching is implemented** — it's a pass-through wrapper
- Delegates directly to `IInstrument` from the SDK
- **Opportunity for new project:** Add real caching (file/DB/Redis) to avoid repeated API calls during backtesting

```csharp
public class CachedInstrument : IInstrument
{
    // All methods delegate directly to the underlying SDK Instrument
    GetCandlesByTime(...)      → instrument.GetCandlesByTime(...)
    GetCandlesByTimeAsync(...) → instrument.GetCandlesByTimeAsync(...)
    GetLastNCandles(...)       → instrument.GetLastNCandles(...)
    GetLastNCandlesAsync(...)  → instrument.GetLastNCandlesAsync(...)
}
```

---

### 2.4 ZoneAnalyzer.PatternAnalysis ⭐ (Core Trading Logic)

**The heart of the system.** Dependencies: Oanda Client.

#### 2.4.1 Candlestick Classification

`CandlestickDataExtensions.GetShape()` classifies each candle:

| Shape | Criteria |
|-------|----------|
| `Boring` | Body < 50% of total range (high-low) |
| `ExcitingRally` | Body ≥ 50% AND close > open (bullish) |
| `ExcitingDrop` | Body ≥ 50% AND close ≤ open (bearish) |

```
ExcitingRally:  │  ║  │    (big green body)
Boring:         │  ─  │    (small body, long wicks)
ExcitingDrop:   │  ║  │    (big red body)
```

#### 2.4.2 Zone Detection — State Machine (ZoneFinder)

The `ZoneFinder` uses a 4-state machine to detect supply/demand zones:

```
States: NotStarted → BuildingLegIn → BuildingBase → BuildingLegOut
```

**Supply Zone** (Rally → Base → Drop):
```
  Price
   ▲    ╱╲        ← Leg-in (ExcitingRally candles)
   │   ╱  ────    ← Base (Boring candles)
   │       ╲      ← Leg-out (ExcitingDrop candles)
   │        ╲
   └──────────→ Time
```

**Demand Zone** (Drop → Base → Rally):
```
  Price
   ▲        ╱
   │       ╱      ← Leg-out (ExcitingRally candles)
   │   ────       ← Base (Boring candles)
   │  ╲          ← Leg-in (ExcitingDrop candles)
   │   ╲╱
   └──────────→ Time
```

**State transitions:**
1. `NotStarted` → scans for first `ExcitingRally` or `ExcitingDrop` candle
2. `BuildingLegIn` → accumulates same-direction exciting candles
3. `BuildingBase` → accumulates `Boring` candles (consolidation); **exciting candles with ≥75% range overlap with the base are absorbed** rather than starting a leg out
4. `BuildingLegOut` → accumulates opposite-direction exciting candles → **ZONE EMITTED**

#### 2.4.2a Zone Freshness & Worked Evaluation

After all zones are detected, a **second pass** evaluates each zone against subsequent candles:

| Property | Type | Logic |
|----------|------|-------|
| `Freshness` | `ZoneFreshness` enum | `Untested` = price never returned; `Tested` = wick entered zone but didn't pierce; `Broken` = wick pierced through |
| `Worked` | `bool?` | `null` if Untested; `true` if tested and price moved ≥2× base width in expected direction; `false` otherwise |

**Supply zone:** Broken if any candle's H > BaseRangeHigh; Tested if H ≥ BaseRangeLow
**Demand zone:** Broken if any candle's L < BaseRangeLow; Tested if L ≤ BaseRangeHigh
**Worked threshold:** Supply = L ≤ BaseRangeLow − 2×width; Demand = H ≥ BaseRangeHigh + 2×width

#### 2.4.2b Base Overlap Tolerance

During `BuildingBase`, if an exciting candle arrives:
1. Calculate what fraction of the candle's H-L range overlaps with the current base
2. If **≥75%** → absorb into base (expand range, increment count, stay in BuildingBase)
3. If **<75%** → treat as leg out (transition to BuildingLegOut)

This prevents false zone boundaries from exciting candles that are mostly contained within the existing consolidation range. A small floating-point tolerance (1e-9) is applied at the 75% boundary.

#### 2.4.3 Zone Configuration & Filtering

```csharp
public class ZoneConfiguration
{
    int MinBaseLength = 3;               // minimum boring candles in base
    double MinLegInToBaseRangeRatio;     // leg-in must be X times base range
    double MinLegOutToBaseRangeRatio;    // leg-out must be X times base range
}
```

✅ **Fixed:** `IsMatch()` now correctly uses `MinLegInToBaseRangeRatio` for leg-in and `MinLegOutToBaseRangeRatio` for leg-out. Division-by-zero guard added for zero-width bases.

#### 2.4.4 Trend Detection (TrendManager)

Uses **swing-based detection** (N-bar pivot) to identify trend direction from the last 60 candles (configurable via `TrendConfiguration`):

1. **Find swing highs** — a candle is a swing high if its High is ≥ all neighboring Highs within `SwingLookback` bars on each side
2. **Find swing lows** — same logic on Lows
3. **Compare last two swing points** of each type:
   - Higher High + Higher Low → **Up**
   - Lower High + Lower Low → **Down**
   - Otherwise → **Sideways**

```csharp
var trend = TrendManager.Create(candles, config);
trend.GetTrendDirection(); // => TrendDirection.Up / Down / Sideways
trend.GetTrend();          // => "Up" / "Down" / "Sideways" (string)
trend.GetSwingHighs();     // => List<SwingPoint> for diagnostics
trend.GetSwingLows();      // => List<SwingPoint> for diagnostics
```

**TrendConfiguration** (configurable via `appsettings.json` / env vars):

| Setting | Default | Description |
|---------|---------|-------------|
| `SwingLookback` | 3 | N-bar pivot lookback (bars on each side) |
| `TrendCandleCount` | 60 | Number of recent candles to analyze |
| `MinSwingPoints` | 2 | Minimum swing highs/lows needed to determine trend |

#### 2.4.5 ZoneManager (Orchestrator)

```csharp
ZoneManager.Create(candles, zoneConfig)
// 1. Sorts candles by time
// 2. Runs ZoneFinder to detect all zones
// 3. Filters by ZoneConfiguration
// 4. Provides GetSupplyZones() / GetDemandZones()

// TODO in source: check whether zone is on correct side of current price
```

#### Entry Points (for reuse)

```csharp
var zones = ZoneManager.Create(candlesticks, new ZoneConfiguration { MinBaseLength = 3 });
var supplyZones = zones.GetSupplyZones();
var demandZones = zones.GetDemandZones();

var trend = TrendManager.Create(candlesticks);
// trend.GetTrendDirection() => TrendDirection.Up / Down / Sideways
// trend.GetTrend()          => "Up" / "Down" / "Sideways"
```

---

### 2.5 Sdk.Playground (Console Demo App)

Interactive CLI that demonstrates the full workflow:

```
Main Menu
├── Accounts
│   ├── List accounts
│   ├── Account details
│   └── Trade Menu
│       ├── View open trades
│       └── Open new trade (instrument, units, direction, trailing SL)
└── Instruments
    ├── Fetch candles (by time range + granularity)
    ├── Print candles as JSON
    ├── Compute zones (ZoneManager)
    └── Compute trend (TrendManager)
```

**Connection setup:** Prompts for OANDA server type + access token at startup.

---

### 2.6 Test Projects

| Project | Status |
|---------|--------|
| `Client.Test` | Auto-generated xUnit stubs — 1250 tests (mostly `// TODO` bodies that pass) |
| `PatternAnalysis.Test` | **39 real tests:** 4 ZoneManager, 7 ZoneFinder freshness/worked, 3 base overlap, 8 sub-zone, 17 TrendManager swing detection |
| `McpServer.Test` | **20 tests:** MCP tool integration tests |
| `Worker.Test` | **63 tests:** InMemoryZoneStore, ConsoleNotificationService, MonitorSettings, zone change detection, candle-aligned scheduling |

#### Test Coverage

| Test Class | Tests | What It Covers |
|------------|-------|----------------|
| `ZoneManagerTests` | 4 | Zone detection with sample JSON data, basic instantiation |
| `ZoneFinderFreshnessTests` | 7 | Freshness (Untested/Tested/Broken) and Worked (null/true/false) for supply and demand zones |
| `ZoneFinderBaseOverlapTests` | 3 | Base absorption of overlapping excited candles at 75% threshold |
| `ZoneFinderSubZoneTests` | 8 | Sub-zone detection: same-type overlap ≥10%, cross-type no match, broken zones excluded |
| `TrendManagerTests` | 17 | Swing-based trend: uptrend/downtrend/sideways detection, swing point identification, config variations, edge cases |
| `InMemoryZoneStoreTests` | 7 | Upsert/get round-trip, overwrite, instrument/granularity isolation, copy semantics |
| `ConsoleNotificationServiceTests` | 7 | FormatAlert output: instrument, type, trend, range, freshness, sub-zone |
| `MonitorSettingsTests` | 6 | Default config values (instruments, granularities, poll interval, cache size) |
| `ZoneChangeDetectionTests` | 10 | Zone key generation, change detection diffing (new/existing/all-new/none-new) |
| `CandleAlignedScheduleTests` | 15+ | Candle-aligned scheduling (M15/H1/H4), granularity mapping, invariants |

---

## 3. Reusable Components for New Project

### 3.1 Direct Reuse (copy/adapt)

| Component | What it gives you | Notes |
|-----------|-------------------|-------|
| **Oanda Client** | Full OANDA V20 API access | Generated code, stable. Consider upgrading to .NET 6/8 |
| **Oanda SDK** | Connection management, candle pagination, trade execution | Clean interfaces, easy to mock |
| **ZoneFinder** | Supply/demand zone detection state machine | Core algorithm, well-structured |
| **TrendManager** | Swing-based trend direction (HH/HL/LH/LL) | Configurable, robust, returns enum + diagnostics |
| **CandlestickShape** | Candle classification (boring/exciting) | Foundation for zone detection |

### 3.2 Needs Enhancement for New Project

| Area | Current State | What's Needed |
|------|---------------|---------------|
| **Caching** | ✅ Worker has `CandleCacheService` (2000 sliding window) | `CachedInstrument` in DataProvider is still a no-op; consider unifying |
| **Notifications** | ✅ ACS Email + console fallback in Worker | Add more channels (SMS, Telegram, push) |
| **Backtesting** | None | Build historical simulation engine using cached candle data |
| **Trade Setup** | Manual via console prompts | Automate: detect zone + trend → generate trade parameters |
| **Configuration** | ✅ Worker uses `appsettings.json` + env vars; MCP uses Key Vault | Playground still uses console prompts |
| **Secret Management** | ✅ Azure Key Vault + Container App secrets | Playground still uses `Config.txt` |
| **Zone Validation** | TODO in source | Check if zone is on correct side of current price |

### 3.3 Key Interfaces to Program Against

```csharp
// Connection
IOandaApiConnection connection = OandaApiConnectionFactory.CreateConnection(type, token);

// Account & Trades
IAccount account = connection.GetAccount(accountId);
ITrades trades = account.Trades;
await trades.OpenTradeAsync(instrument, units, direction, trailingSL);

// Candles
IInstrument instrument = connection.GetInstrument(instrumentName);
var candles = await instrument.GetCandlesByTimeAsync(granularity, from, to, components);

// Analysis
var zoneManager = ZoneManager.Create(candles, config);
var trend = TrendManager.Create(candles);
```

---

## 4. Data Flow for a Typical Trade Setup

```
1. Connect to OANDA API (token + server type)
         │
2. Select account + instrument (e.g., EUR_USD)
         │
3. Fetch candles (e.g., H1 for last 30 days)
         │
4. Classify each candle → Boring / ExcitingRally / ExcitingDrop
         │
5. Run ZoneFinder state machine → detect supply/demand zones
         │
6. Filter zones by ZoneConfiguration (min base length, leg ratios)
         │
7. Determine trend via TrendManager (swing-based: Higher Highs/Lows detection)
         │
8. [NEW] Match zones to current price + trend direction
         │
9. [NEW] Generate trade setup (entry, SL, TP)
         │
10. [NEW] Send notification / execute trade
         │
11. [NEW] Log for backtesting analysis
```

---

## 5. File Inventory

```
├── .github/
│   └── workflows/
│       └── deploy.yml                      # CI/CD: test → deploy (Bicep + ACR + Container Apps)
├── infra/
│   ├── main.bicep                          # Subscription-level orchestrator
│   └── modules/
│       └── resources.bicep                 # All Azure resources (ACR, Key Vault, Storage, ACS,
│                                           #   Email Service, Container Apps Env, MCP app, worker app)
├── scripts/
│   └── deploy.sh                           # Manual deployment helper
├── Dockerfile                              # MCP server multi-stage build
├── Dockerfile.worker                       # Worker service multi-stage build
└── src/
    ├── ZoneAnalyzer.sln
    ├── GeriRemenyi.Oanda.V20.Client/          # ~202 files (auto-generated)
    │   ├── Api/                                # 7 API endpoint classes
    │   ├── Client/                             # 15 HTTP runtime classes
    │   └── Model/                              # 180 DTO/enum classes (incl. Zone.cs with SubZone)
    ├── GeriRemenyi.Oanda.V20.Client.Test/      # Generated test stubs (1250 tests)
    ├── GeriRemenyi.Oanda.V20.Sdk/              # 21 files
    │   ├── Account/                            # Account.cs, IAccount.cs
    │   ├── Instrument/                         # Instrument.cs, IInstrument.cs
    │   ├── Trade/                              # Trades.cs, ITrades.cs, TradeDirection.cs
    │   └── Common/                             # Extensions, Exceptions, Types
    ├── GeriRemenyi.Oanda.V20.Sdk.Playground/   # 8 files (console demo)
    ├── ZoneAnalyzer.DataProvider/              # 2 files (CachedInstrument wrapper)
    ├── ZoneAnalyzer.PatternAnalysis/           # 8 files (core analysis)
    │   ├── ZoneManager.cs                      # Orchestrator
    │   ├── ZoneFinder.cs                       # State machine + EvaluateSubZones()
    │   ├── TrendManager.cs                     # Swing-based trend detection
    │   ├── TrendConfiguration.cs               # Trend config (SwingLookback, TrendCandleCount, MinSwingPoints)
    │   ├── CandlestickShape.cs                 # Candle classification enum
    │   ├── CandlestickDataExtensions.cs        # Shape classification logic
    │   ├── ZoneConfiguration.cs                # Zone filter config (MaxBaseLength=6)
    │   └── SampleCandleSticks.json             # Test fixture data
    ├── ZoneAnalyzer.PatternAnalysis.Test/      # 5 test classes (39 tests)
    │   ├── ZoneManagerTests.cs
    │   ├── ZoneFinderFreshnessTests.cs
    │   ├── ZoneFinderBaseOverlapTests.cs
    │   ├── ZoneFinderSubZoneTests.cs
    │   └── TrendManagerTests.cs
    ├── ForexZoneAnalyzer.McpServer/            # MCP server (11 tools)
    │   ├── Program.cs                          # Host builder, Entra ID auth, MapMcp("/mcp")
    │   └── Tools/
    │       └── InstrumentTools.cs              # All 11 MCP tool methods
    ├── ForexZoneAnalyzer.McpServer.Test/       # 20 integration tests
    ├── ForexZoneAnalyzer.Worker/               # Background zone monitor
    │   ├── Program.cs                          # Host builder, DI wiring
    │   ├── Configuration/
    │   │   └── MonitorSettings.cs              # Config POCO (instruments, intervals)
    │   └── Services/
    │       ├── ZoneMonitorService.cs           # BackgroundService polling loop
    │       ├── CandleCacheService.cs           # Incremental 2000-candle cache
    │       ├── OandaConnectionService.cs       # OANDA connection singleton
    │       ├── IZoneStore.cs                   # Zone persistence interface
    │       ├── InMemoryZoneStore.cs            # Dev zone store
    │       ├── TableStorageZoneStore.cs        # Azure Table Storage zone store
    │       ├── INotificationService.cs         # Notification interface
    │       ├── ConsoleNotificationService.cs   # Console output (dev)
    │       └── EmailNotificationService.cs     # ACS Email (production)
    └── ForexZoneAnalyzer.Worker.Test/          # 63 tests
        ├── InMemoryZoneStoreTests.cs
        ├── ConsoleNotificationServiceTests.cs
        ├── MonitorSettingsTests.cs
        ├── ZoneChangeDetectionTests.cs
        └── CandleAlignedScheduleTests.cs
```

---

## 6. External Dependencies

| Package | Version | Used By | Purpose |
|---------|---------|---------|---------|
| RestSharp | 112.1.0 | Client | HTTP requests to OANDA |
| Newtonsoft.Json | 13.0.3 | Client, Worker | JSON serialization |
| JsonSubTypes | 2.0.0 | Client | Polymorphic deserialization |
| System.ComponentModel.Annotations | 5.0.0 | Client | Model validation |
| ModelContextProtocol | 0.1.0-preview.13 | McpServer | MCP protocol server implementation |
| Microsoft.Identity.Web | 3.8.5 | McpServer | Entra ID JWT bearer authentication |
| Azure.Identity | 1.14.2 | McpServer, Worker | DefaultAzureCredential for Azure auth |
| Azure.Security.KeyVault.Secrets | 4.7.0 | McpServer | OANDA token retrieval from Key Vault |
| Azure.Data.Tables | 12.9.1 | Worker | Azure Table Storage for zone persistence |
| Azure.Communication.Email | 1.0.1 | Worker | ACS email notifications |
| Microsoft.Extensions.Hosting | 10.0.0 | Worker | .NET 10 Worker SDK BackgroundService |
| Microsoft.Extensions.Caching.Memory | 10.0.0 | Worker | In-memory candle cache |
| xUnit | 2.9.3 | Test projects | Unit testing |

---

## 7. Known Issues & Technical Debt

### Resolved ✅

1. ~~**ZoneConfiguration bug**~~ — Fixed: `IsMatch()` now uses correct ratio for each leg + division-by-zero guard
2. ~~**Hardcoded test path**~~ — Fixed: uses assembly-relative `Path.Combine()` + `CopyToOutputDirectory`
3. ~~**Zero-range candle crash**~~ — Fixed: `GetShape()` guards `range > 0`, classifies zero-range as Boring
4. ~~**DateTime culture-sensitivity**~~ — Fixed: all 29 `DateTime.Parse()` calls use `CultureInfo.InvariantCulture`
5. ~~**Lazy LINQ re-evaluation**~~ — Fixed: supply/demand zone collections materialized with `.ToList()`
6. ~~**EOL framework**~~ — ✅ Upgraded to .NET 10
7. ~~**RestSharp vulnerability**~~ — ✅ Upgraded to RestSharp 112.1.0
8. ~~**No caching**~~ — ✅ Worker has `CandleCacheService` with 2000-candle sliding window + incremental fetching
9. ~~**No notifications**~~ — ✅ ACS Email notifications in Worker (HTML + plain text), console fallback in dev
10. ~~**No configuration**~~ — ✅ Worker uses `appsettings.json` + environment variables; MCP server uses Key Vault
11. ~~**Secret management**~~ — ✅ Azure Key Vault for OANDA token; Container App secrets for ACS connection string
12. ~~**TrendManager fragility**~~ — ✅ Rewritten with swing-based detection; returns `TrendDirection` enum, handles edge cases (< 2 candles → Sideways), configurable via `TrendConfiguration`

### Remaining

1. **No zone price validation** — TODO in source: check if zone is on correct side of current price
2. **Empty tests** — Client.Test has 1,250 stub tests with `// TODO` bodies
3. **No error handling in Playground** — minimal try/catch around API calls
4. **CachedInstrument no-op** — `CachedInstrument` in DataProvider still doesn't cache; Worker's `CandleCacheService` is separate
5. **Playground secret handling** — still uses `Config.txt` (gitignored) instead of a proper secret store
6. **Bicep warnings (non-blocking)** — `no-hardcoded-env-urls` for `login.microsoftonline.com`, `BCP318` for conditional null check

---

## 8. Recommendations for New Project

### Current Architecture (Implemented)

```
forex-zone-analyzer
├── PatternAnalysis/         # Zone detection, swing-based trend, candle classification
├── Oanda Client + SDK/      # Full OANDA V20 API: candles, accounts, trades
├── MCP Server/              # 11 tools exposed via MCP protocol (Entra ID auth)
├── Worker/                  # Background monitoring: cache → detect → notify
│   ├── Caching/             # CandleCacheService (2000 sliding window)
│   ├── Persistence/         # IZoneStore (Table Storage / in-memory)
│   └── Notifications/       # INotificationService (ACS Email / console)
├── Infra/                   # Bicep: ACR, Key Vault, Storage, ACS, Container Apps
├── CI/CD/                   # GitHub Actions with OIDC + Bicep deploy
└── Tests/                   # 1,372 tests across 4 test projects
```

### Potential Enhancements

| Priority | Enhancement | Details |
|----------|-------------|---------|
| High | **Backtesting engine** | Historical simulation using cached candle data to validate zone strategies |
| High | **Trade setup automation** | Zone + trend + price proximity → automated trade parameter generation |
| Medium | **Zone price validation** | Check if zone is on correct side of current price (TODO in source) |
| Medium | **More notification channels** | SMS, Telegram, push notifications via ACS or third-party |
| Low | **Unify caching** | Merge `CachedInstrument` no-op with Worker's `CandleCacheService` |
| Low | **Dashboard API** | REST/gRPC API for real-time zone visualization |
| Low | **Clean up Client.Test** | Fill in 1,250 stub test bodies or remove stubs |
