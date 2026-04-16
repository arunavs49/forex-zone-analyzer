# ZoneRadar — iOS App

A native SwiftUI iPhone app that visualizes supply/demand zones on interactive candlestick charts and enables zone-based limit order placement. Connects to the Forex Zone Analyzer MCP server deployed on Azure Container Apps for real-time candle data, pre-computed zone analysis, trend direction, and trade management.

## Features

- 📊 **Interactive candlestick charts** with pinch-to-zoom, pan, and crosshair gestures
- 🟢 **Demand zone overlays** (green) and 🔴 **supply zone overlays** (red) rendered directly on charts
- 📋 **Zone detail list** showing freshness (Untested/Tested/Broken), worked status, base range, and sub-zone flags
- 📈 **Trend indicator** (Up/Down/Sideways) via swing-based detection
- ⏱️ **Multiple timeframes** — M5, M15, M30, H1, H4, Daily
- 💱 **20 forex pairs** — 7 major USD pairs and 13 crosses
- 💹 **Zone-based trading** — tap a zone on the chart to place a limit order with auto-calculated entry, stop loss, and take profit
- 📑 **Pending orders** — view and cancel open pending orders
- 🔐 **Entra ID authentication** — MSAL-based sign-in with automatic silent token refresh and interactive fallback
- 🔌 **MCP protocol client** — native Streamable HTTP (JSON-RPC 2.0) transport with automatic session recovery on 404
- 🔔 **Zone alerts** — background polling with local notifications for new untested/tested zones

## Architecture

```
┌──────────────────────────────────────────┐
│         iPhone App (SwiftUI)              │
│  ┌─────────────────────────────────────┐ │
│  │ InstrumentListView                  │ │
│  │  └── ChartContainerView             │ │
│  │       ├── CandlestickChartView      │ │
│  │       │   ├── PriceGridView         │ │
│  │       │   ├── ZoneOverlayView       │ │
│  │       │   └── CrosshairOverlay      │ │
│  │       ├── ZoneListView (sheet)      │ │
│  │       │   └── PlaceOrderSheet       │ │
│  │       └── PendingOrdersView (sheet) │ │
│  ├─────────────────────────────────────┤ │
│  │ AuthService (MSAL / Entra ID)       │ │
│  │ ChartViewModel                      │ │
│  │  └── ForexDataService               │ │
│  │       └── MCPClient                 │ │
│  │           (JSON-RPC 2.0 /mcp)       │ │
│  │           (auto session recovery)   │ │
│  │ ZonePollingService                  │ │
│  │  └── local notifications            │ │
│  └─────────────────────────────────────┘ │
└──────────────┬───────────────────────────┘
               │ HTTPS POST (Bearer JWT)
               ▼
┌──────────────────────────────────────────┐
│  Azure Container Apps                     │
│  ForexZoneAnalyzer.McpServer              │
│  • get_candles       • get_stored_zones   │
│  • place_limit_order • get_pending_orders │
│  • cancel_order      • list_accounts      │
└──────────────────────────────────────────┘
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
open ZoneRadar.xcodeproj
```

### Step 2: Configure Signing (One-Time Setup)

1. In Xcode, select the **ZoneRadar** project in the navigator (blue icon at the top)
2. Select the **ZoneRadar** target
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

Once the app is running on your iPhone, tap the **⚙️ gear icon** and configure the connection. You have two options:

#### Option A: Connect to Azure (Production) — Entra ID Sign-In

Use this if you've deployed the MCP server to Azure Container Apps. The app uses MSAL (Microsoft Authentication Library) for Entra ID sign-in with automatic silent token refresh.

1. **Get your MCP server URL:**
   ```bash
   FQDN=$(az containerapp show --name ca-forex-mcp --resource-group rg-forex-mcp \
     --query properties.configuration.ingress.fqdn -o tsv)
   echo "https://${FQDN}/mcp"
   ```
2. In the app settings, enter the **Server URL**: `https://<fqdn>/mcp`
3. Tap **Sign in with Microsoft** — the app handles token acquisition and refresh automatically
4. Select your **OANDA Account ID** (the app fetches available accounts after sign-in)
5. Tap **Done**

> **Note:** The app silently refreshes tokens. If the refresh token expires, an interactive sign-in prompt appears automatically.

#### Option A (fallback): Manual Bearer Token

If MSAL sign-in is unavailable, you can paste a token manually:

1. **Get an Entra ID bearer token:**
   ```bash
   az account get-access-token --resource api://<your-app-client-id> --query accessToken -o tsv
   ```
2. In the app settings, enter the **Server URL** and paste the **Bearer Token**
3. Tap **Done**

> **Note:** Manual tokens expire after ~1 hour and must be refreshed manually.

#### Option B: Connect to Local Dev Server

Use this to test against the MCP server running on your Mac. No Entra ID token is needed (auth is disabled in dev mode).

1. **Set up user secrets** (one-time):
   ```bash
   cd src/ForexZoneAnalyzer.McpServer
   dotnet user-secrets init
   dotnet user-secrets set "Oanda:ApiToken" "<your-oanda-api-token>"
   dotnet user-secrets set "Oanda:ConnectionType" "FxPractice"
   ```
2. **Start the MCP server** bound to all interfaces:
   ```bash
   dotnet run --urls "http://0.0.0.0:5000"
   ```
3. **Find your Mac's local IP:**
   ```bash
   ipconfig getifaddr en0
   ```
   This returns something like `192.168.1.42`.
4. In the app settings, enter:
   - **Server URL**: `http://192.168.1.42:5000/mcp` (use your Mac's actual IP)
   - **Bearer Token**: leave empty
5. Tap **Done**

> **Important:** Your iPhone and Mac must be on the same Wi-Fi network. The app includes `NSAllowsLocalNetworking` so HTTP connections to local network addresses are permitted.

### Step 7: Use the App

1. Select a **currency pair** from the list (e.g., EUR/USD)
2. Choose a **timeframe** using the segmented picker (e.g., H1)
3. The app fetches live candle data and pre-computed zones/trend from the MCP server
4. **Pinch** to zoom in/out, **drag** to pan the chart
5. **Tap a zone** on the chart to open the order placement sheet with auto-calculated entry, stop loss, and take profit
6. Tap the **list icon** (top-right) to see detailed zone information and place orders
7. Tap the **orders icon** to view and cancel pending orders
8. Tap **↻** to refresh data

---

## Zone Alerts (Background Polling)

The app polls the MCP server at a configurable interval and fires **local notifications** when new zones are detected. No Apple Developer account or APNs credentials needed.

### How It Works
1. When the app is open, a timer checks all instruments for new untested/tested zones
2. On first poll, it builds a baseline of known zones (no alerts)
3. Subsequent polls compare against the baseline — new zones trigger a local notification
4. Notifications appear as banners even when the app is in the foreground

### Configuration
In **Settings → Zone Alerts**:
- **Enable zone polling** — toggle on/off
- **Check interval** — 5, 15, 30, or 60 minutes (default: 15)

> **Note:** iOS limits background execution. Polling only runs reliably while the app is open. For alerts when the app is closed, the Worker still sends **email notifications** via Azure Communication Services.

---

## Troubleshooting

### "Untrusted Developer" error
Go to **Settings → General → VPN & Device Management** on your iPhone and trust the developer certificate.

### App won't install — "device not available"
Make sure Developer Mode is enabled (Step 4) and the device is unlocked when deploying.

### "Could not launch" — app crashes on start
Ensure your MCP server URL is correct and reachable from your iPhone's network.

### Token expired
The app automatically refreshes tokens via MSAL silent renewal. If the refresh token itself expires, an interactive sign-in prompt appears. For manual bearer tokens, generate a fresh one using:
```bash
az account get-access-token --resource api://<your-app-client-id> --query accessToken -o tsv
```

### "Session not found" (404) errors
The MCP client automatically recovers from expired server sessions by clearing the stale session ID, re-initializing, and retrying the request. If errors persist, check that the MCP server is running.

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
├── ZoneRadar.xcodeproj/           # Xcode project
└── ForexZoneApp/
    ├── ZoneRadarApp.swift          # App entry point, scene lifecycle, background tasks
    ├── ContentView.swift           # Root navigation + settings/pending-orders sheets
    ├── Info.plist                   # App config (MSAL redirect, background modes)
    ├── ZoneRadar.entitlements       # Keychain sharing for MSAL token cache
    ├── Models/
    │   ├── Candle.swift             # OHLCV candlestick model
    │   ├── Zone.swift               # Supply/demand zone model + order parameter calculation
    │   ├── ZoneAnalysisResponse.swift  # MCP tool response model
    │   ├── Instrument.swift         # Currency pair + granularity enums
    │   └── AppSettings.swift        # UserDefaults-backed settings (URL, account, risk)
    ├── Services/
    │   ├── MCPClient.swift          # MCP Streamable HTTP client (JSON-RPC 2.0) with session recovery
    │   ├── ForexDataService.swift   # Typed wrapper for MCP tool calls
    │   ├── AuthService.swift        # MSAL / Entra ID sign-in with silent refresh
    │   └── ZonePollingService.swift # Background polling + local notifications
    ├── ViewModels/
    │   └── ChartViewModel.swift     # Async data fetching + chart state
    ├── Views/
    │   ├── InstrumentListView.swift    # Pair picker (7 majors + 13 crosses)
    │   ├── ChartContainerView.swift    # Chart + controls + trend badge + trade bar
    │   ├── ZoneListView.swift          # Zone detail list with order placement
    │   ├── PlaceOrderSheet.swift       # Limit order form with auto-calculated parameters
    │   ├── PendingOrdersView.swift     # View and cancel open pending orders
    │   ├── SettingsView.swift          # MCP URL, auth, account, risk, polling config
    │   └── Components/
    │       ├── CandlestickChartView.swift  # Custom Canvas chart renderer
    │       ├── ZoneOverlayView.swift       # Zone rectangle overlay
    │       ├── CrosshairOverlay.swift      # Crosshair + zone tap selection
    │       └── PriceGridView.swift         # Price axis grid lines
    └── Assets.xcassets/             # App icons and assets
```

## MCP Tools Used

| Tool | Purpose |
|------|---------|
| `get_candles` | Fetch OHLCV candlestick data for chart rendering |
| `get_stored_zones` | Fetch pre-computed zones and trend from Table Storage |
| `place_limit_order` | Place a limit order derived from zone parameters |
| `get_pending_orders` | List pending (unfilled) orders for an account |
| `cancel_order` | Cancel a pending order by ID |
| `list_accounts` | Fetch available OANDA account IDs for settings |
