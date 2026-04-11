# Forex Zone Analyzer — iOS App

A native SwiftUI iPhone app that visualizes supply/demand zones on interactive candlestick charts. Connects to the Forex Zone Analyzer MCP server deployed on Azure Container Apps to fetch real-time candle data, zone analysis, and trend direction.

## Features

- 📊 **Interactive candlestick charts** with pinch-to-zoom and pan gestures
- 🟢 **Demand zone overlays** (green) and 🔴 **supply zone overlays** (red) rendered directly on charts
- 📋 **Zone detail list** showing freshness (Untested/Tested/Broken), worked status, base range, and sub-zone flags
- 📈 **Trend indicator** (Up/Down/Sideways) via linear regression
- ⏱️ **Multiple timeframes** — 5min, 15min, 30min, 1H, 4H, Daily
- 💱 **20 forex pairs** — all majors and popular crosses
- 🔐 **Entra ID authentication** — configurable bearer token for the cloud MCP server
- 🔌 **MCP protocol client** — native Streamable HTTP (JSON-RPC 2.0) transport

## Architecture

```
┌─────────────────────────────────────┐
│         iPhone App (SwiftUI)         │
│  ┌────────────────────────────────┐ │
│  │ InstrumentListView             │ │
│  │  └── ChartContainerView        │ │
│  │       ├── CandlestickChartView │ │
│  │       │   ├── PriceGridView    │ │
│  │       │   └── ZoneOverlayView  │ │
│  │       └── ZoneListView (sheet) │ │
│  ├────────────────────────────────┤ │
│  │ ChartViewModel                 │ │
│  │  └── ForexDataService          │ │
│  │       └── MCPClient            │ │
│  │           (JSON-RPC 2.0 /mcp)  │ │
│  └────────────────────────────────┘ │
└──────────────┬──────────────────────┘
               │ HTTPS POST
               ▼
┌──────────────────────────────────────┐
│  Azure Container Apps                │
│  ForexZoneAnalyzer.McpServer         │
│  • get_candles                       │
│  • get_supply_demand_zones           │
│  • get_trend                         │
└──────────────────────────────────────┘
```

## Prerequisites

- **macOS** with [Xcode](https://developer.apple.com/xcode/) installed (version 15.0 or later)
- **iPhone** running iOS 17.0 or later
- **Apple ID** (free Apple Developer account is sufficient for personal device deployment)
- **MCP server** deployed and accessible (see main project README)

---

## Deploy to Your iPhone

### Step 1: Open the Project in Xcode

```bash
cd ios/ForexZoneApp
open ForexZoneApp.xcodeproj
```

### Step 2: Configure Signing (One-Time Setup)

1. In Xcode, select the **ForexZoneApp** project in the navigator (blue icon at the top)
2. Select the **ForexZoneApp** target
3. Go to the **Signing & Capabilities** tab
4. Check **"Automatically manage signing"**
5. Under **Team**, select your Apple ID:
   - If you don't see your Apple ID, go to **Xcode → Settings → Accounts** and add it
   - A free Apple ID works — you don't need a paid Developer Program membership
6. Xcode will automatically create a provisioning profile for your device

> **Note:** With a free Apple ID, the app expires after 7 days and must be re-deployed. A paid Apple Developer Program account ($99/year) removes this limit.

### Step 3: Connect Your iPhone

1. Connect your iPhone to your Mac via USB cable (or USB-C)
2. If prompted on the iPhone, tap **"Trust This Computer"** and enter your passcode
3. In Xcode's toolbar, click the device selector (next to the play button) and select your iPhone
   - Your device should appear under **iOS Devices** (not Simulators)
4. If this is the first time, wait for Xcode to prepare the device (this can take a few minutes)

### Step 4: Enable Developer Mode on iPhone (iOS 16+)

If you haven't already:

1. On your iPhone, go to **Settings → Privacy & Security → Developer Mode**
2. Toggle **Developer Mode** ON
3. Your iPhone will restart
4. After restart, confirm the prompt to enable Developer Mode

### Step 5: Build and Run

1. In Xcode, ensure your iPhone is selected as the run destination
2. Press **⌘R** (Command + R) or click the **▶ Play** button
3. The first time, you may see an error about an untrusted developer:
   - On your iPhone, go to **Settings → General → VPN & Device Management**
   - Tap on your Apple ID under **Developer App**
   - Tap **"Trust"** and confirm
4. Run again with **⌘R** — the app will install and launch on your device

### Step 6: Configure the MCP Server

Once the app is running on your iPhone:

1. Tap the **⚙️ gear icon** in the top-right corner
2. Enter your **MCP Server URL**:
   ```
   https://<your-container-app>.azurecontainerapps.io/mcp
   ```
3. Enter your **Entra ID Bearer Token**:
   - Get a token using the Azure CLI:
     ```bash
     az account get-access-token --resource api://<your-app-client-id> --query accessToken -o tsv
     ```
   - Or use the included script: `./scripts/get-mcp-token.sh`
4. Tap **Done**

### Step 7: Use the App

1. Select a **currency pair** from the list (e.g., EUR/USD)
2. Choose a **timeframe** using the segmented picker (e.g., H1)
3. The app will fetch candle data and zones from your MCP server
4. **Pinch** to zoom in/out, **drag** to pan the chart
5. Tap the **list icon** (top-right) to see detailed zone information
6. Tap **↻** to refresh data

---

## Troubleshooting

### "Untrusted Developer" error
Go to **Settings → General → VPN & Device Management** on your iPhone and trust the developer certificate.

### App won't install — "device not available"
Make sure Developer Mode is enabled (Step 4) and the device is unlocked when deploying.

### "Could not launch" — app crashes on start
Ensure your MCP server URL is correct and reachable from your iPhone's network.

### Token expired
Entra ID tokens expire (typically after 1 hour). Generate a fresh token using:
```bash
az account get-access-token --resource api://<your-app-client-id> --query accessToken -o tsv
```

### No data showing
- Verify the MCP server is running: `curl https://<your-url>/health`
- Check that the OANDA API token is valid in Key Vault
- Forex markets are closed on weekends — some candle data may be limited

### Build errors in Xcode
- Ensure Xcode is version 15.0+ with iOS 17+ SDK
- Clean build folder: **Product → Clean Build Folder** (⌘⇧K)
- If targeting a newer iOS version, update `IPHONEOS_DEPLOYMENT_TARGET` in project settings

---

## Project Structure

```
ios/ForexZoneApp/
├── ForexZoneApp.xcodeproj/        # Xcode project
└── ForexZoneApp/
    ├── ForexZoneAppApp.swift       # App entry point
    ├── ContentView.swift           # Root navigation + settings sheet
    ├── Models/
    │   ├── Candle.swift            # OHLCV candlestick model
    │   ├── Zone.swift              # Supply/demand zone model
    │   ├── ZoneAnalysisResponse.swift  # MCP tool response model
    │   ├── Instrument.swift        # Currency pair + granularity enums
    │   └── AppSettings.swift       # UserDefaults-backed settings
    ├── Services/
    │   ├── MCPClient.swift         # MCP Streamable HTTP client (JSON-RPC 2.0)
    │   └── ForexDataService.swift  # Typed wrapper for MCP tool calls
    ├── ViewModels/
    │   └── ChartViewModel.swift    # Async data fetching + state
    ├── Views/
    │   ├── InstrumentListView.swift    # Pair picker (majors + crosses)
    │   ├── ChartContainerView.swift    # Chart + controls + trend badge
    │   ├── ZoneListView.swift          # Zone detail list (sheet)
    │   ├── SettingsView.swift          # MCP URL + token configuration
    │   └── Components/
    │       ├── CandlestickChartView.swift  # Custom Canvas chart renderer
    │       ├── ZoneOverlayView.swift       # Zone rectangle overlay
    │       └── PriceGridView.swift         # Price axis grid lines
    └── Assets.xcassets/            # App icons and assets
```

## MCP Tools Used

| Tool | Purpose |
|------|---------|
| `get_candles` | Fetch OHLCV candlestick data for chart rendering |
| `get_supply_demand_zones` | Detect supply/demand zones with freshness analysis |
| `get_trend` | Get trend direction via linear regression |
