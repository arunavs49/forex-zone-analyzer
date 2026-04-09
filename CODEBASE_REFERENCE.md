# forex-zone-analyzer — Comprehensive Codebase Documentation

> **Purpose:** Reference document for building a new project that reuses this codebase's
> trade setup, notification, and backtesting capabilities.

---

## 1. High-Level Overview

| Aspect        | Details |
|---------------|---------|
| **Language**  | C# (.NET Core 3.1) |
| **Solution**  | `src/ZoneAnalyzer.sln` — 7 projects |
| **Broker**    | OANDA V20 REST API |
| **Auth**      | Bearer token (Personal Access Token) |
| **Core Idea** | Detect supply/demand zones from candlestick patterns and trend direction to identify trade setups |

### Architecture Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                   Sdk.Playground (Console App)               │
│         Interactive menus: accounts, instruments, trades     │
├──────────────────────────┬───────────────────────────────────┤
│  ZoneAnalyzer            │  GeriRemenyi.Oanda.V20.Sdk        │
│  .PatternAnalysis        │  (SDK wrapper)                    │
│  ─────────────────       │  ───────────────                  │
│  • ZoneManager           │  • OandaApiConnection             │
│  • ZoneFinder            │  • Account / IAccount             │
│  • TrendManager          │  • Instrument / IInstrument       │
│  • CandlestickShape      │  • Trades / ITrades               │
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
```

---

## 2. Project-by-Project Breakdown

### 2.1 GeriRemenyi.Oanda.V20.Client (Auto-generated API Client)

- **Source:** OpenAPI Generator 3.0.25
- **~202 .cs files** (7 API classes, 15 runtime/client classes, 180 model DTOs)
- **Dependencies:** RestSharp 106.10, Newtonsoft.Json 12.0, JsonSubTypes 1.5

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

**The heart of the system.** Dependencies: Oanda Client + **MathNet.Numerics** (for linear regression).

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
3. `BuildingBase` → accumulates `Boring` candles (consolidation)
4. `BuildingLegOut` → accumulates opposite-direction exciting candles → **ZONE EMITTED**

#### 2.4.3 Zone Configuration & Filtering

```csharp
public class ZoneConfiguration
{
    int MinBaseLength = 3;               // minimum boring candles in base
    double MinLegInToBaseRangeRatio;     // leg-in must be X times base range
    double MinLegOutToBaseRangeRatio;    // leg-out must be X times base range
}
```

**⚠️ Known Bug:** `IsMatch()` uses `MinLegOutToBaseRangeRatio` for BOTH leg-in and leg-out checks. `MinLegInToBaseRangeRatio` is declared but never used.

#### 2.4.4 Trend Detection (TrendManager)

Uses **linear regression** (1st-degree polynomial fit) on up to the last 60 closing prices:

```csharp
// MathNet.Numerics.Fit.Polynomial(xData, yData, degree: 1)
// Returns coefficients [intercept, slope]
// slope > 0  → Trend.Up
// slope < 0  → Trend.Down
// slope == 0 → Trend.Sideways
```

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
// trend.Direction => Up / Down / Sideways
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
| `Client.Test` | Auto-generated xUnit stubs — 176 model tests + 7 API tests, all `// TODO` bodies |
| `PatternAnalysis.Test` | 1 real test: `GetSupplyZonesTest()` asserts non-null. Uses `SampleCandleSticks.json` (145 hourly candles, Feb 17-27 2023). **⚠️ Hardcoded Windows path.** |

---

## 3. Reusable Components for New Project

### 3.1 Direct Reuse (copy/adapt)

| Component | What it gives you | Notes |
|-----------|-------------------|-------|
| **Oanda Client** | Full OANDA V20 API access | Generated code, stable. Consider upgrading to .NET 6/8 |
| **Oanda SDK** | Connection management, candle pagination, trade execution | Clean interfaces, easy to mock |
| **ZoneFinder** | Supply/demand zone detection state machine | Core algorithm, well-structured |
| **TrendManager** | Trend direction via linear regression | Simple, effective |
| **CandlestickShape** | Candle classification (boring/exciting) | Foundation for zone detection |

### 3.2 Needs Enhancement for New Project

| Area | Current State | What's Needed |
|------|---------------|---------------|
| **Caching** | `CachedInstrument` is a no-op wrapper | Add real caching for backtesting (file/SQLite/Redis) |
| **Notifications** | None | Add alerts (email, SMS, push, Telegram) when zones form near price |
| **Backtesting** | None | Build historical simulation engine using cached candle data |
| **Trade Setup** | Manual via console prompts | Automate: detect zone + trend → generate trade parameters |
| **Configuration** | Hardcoded / console prompts | Use config files / environment variables |
| **Zone Bug** | `MinLegInToBaseRangeRatio` unused | Fix `ZoneConfiguration.IsMatch()` |
| **Tests** | Mostly stubs | Write real unit + integration tests |
| **Target Framework** | .NET Core 3.1 (EOL) | Upgrade to .NET 8+ |
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
7. Determine trend via TrendManager (linear regression on closes)
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
src/
├── ZoneAnalyzer.sln
├── GeriRemenyi.Oanda.V20.Client/          # ~202 files (auto-generated)
│   ├── Api/                                # 7 API endpoint classes
│   ├── Client/                             # 15 HTTP runtime classes
│   └── Model/                              # 180 DTO/enum classes
├── GeriRemenyi.Oanda.V20.Client.Test/      # Generated test stubs
├── GeriRemenyi.Oanda.V20.Sdk/              # 21 files
│   ├── Account/                            # Account.cs, IAccount.cs
│   ├── Instrument/                         # Instrument.cs, IInstrument.cs
│   ├── Trade/                              # Trades.cs, ITrades.cs, TradeDirection.cs
│   └── Common/                             # Extensions, Exceptions, Types
├── GeriRemenyi.Oanda.V20.Sdk.Playground/   # 8 files (console demo)
├── ZoneAnalyzer.DataProvider/              # 2 files (CachedInstrument wrapper)
├── ZoneAnalyzer.PatternAnalysis/           # 7 files (core analysis)
│   ├── ZoneManager.cs                      # Orchestrator
│   ├── ZoneFinder.cs                       # State machine zone detector
│   ├── TrendManager.cs                     # Linear regression trend
│   ├── CandlestickShape.cs                 # Candle classification enum
│   ├── CandlestickDataExtensions.cs        # Shape classification logic
│   ├── ZoneConfiguration.cs                # Zone filter config
│   └── SampleCandleSticks.json             # Test fixture data
└── ZoneAnalyzer.PatternAnalysis.Test/      # 3 files (minimal tests)
```

---

## 6. External Dependencies

| Package | Version | Used By | Purpose |
|---------|---------|---------|---------|
| RestSharp | 106.10.1 | Client | HTTP requests to OANDA |
| Newtonsoft.Json | 12.0.1 | Client | JSON serialization |
| JsonSubTypes | 1.5.2 | Client | Polymorphic deserialization |
| System.ComponentModel.Annotations | 4.5.0 | Client | Model validation |
| MathNet.Numerics | (unspecified) | PatternAnalysis | Polynomial fitting for trend detection |
| xUnit | (unspecified) | Test projects | Unit testing |

---

## 7. Known Issues & Technical Debt

1. **ZoneConfiguration bug** — `MinLegInToBaseRangeRatio` is declared but never used in `IsMatch()`
2. **Hardcoded test path** — `ZoneManagerTests.cs` uses `C:\Enlistments\...` Windows path
3. **No real caching** — `CachedInstrument` doesn't cache anything
4. **No zone price validation** — TODO: check if zone is on correct side of current price
5. **EOL framework** — .NET Core 3.1 is end-of-life; upgrade recommended
6. **Empty tests** — 180+ test stubs with `// TODO` bodies
7. **No error handling in Playground** — minimal try/catch around API calls
8. **Secret management** — `Config.txt` gitignored but no proper secret store

---

## 8. Recommendations for New Project

### Architecture

```
New Project (forex-trade-engine)
├── Core/                    # Reuse: ZoneFinder, TrendManager, CandlestickShape
├── Data/                    # Enhanced: real caching layer for candles
├── Broker/                  # Reuse: Oanda Client + SDK (upgrade to .NET 8)
├── Strategy/                # NEW: trade setup rules, entry/exit logic
├── Backtesting/             # NEW: historical simulation engine
├── Notifications/           # NEW: alert system (Telegram, email, etc.)
├── API/                     # NEW: REST/gRPC API for dashboards
└── Tests/                   # NEW: comprehensive test suite
```

### Priority Migration Path

1. **Port PatternAnalysis** — ZoneFinder + TrendManager (core value)
2. **Port SDK** — connection + candle fetching + trade execution
3. **Add caching** — essential for backtesting without hammering OANDA API
4. **Build strategy engine** — zone + trend → trade setup automation
5. **Add notifications** — alert when trade setups form
6. **Build backtester** — replay historical data through strategy engine
