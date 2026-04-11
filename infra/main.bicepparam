using 'main.bicep'

param location = 'eastus2'
param baseName = 'forex-mcp'
param oandaApiToken = ''          // Provide at deployment time: --parameters oandaApiToken=<token>
param oandaConnectionType = 'FxPractice'
param entraIdTenantId = ''        // Your Azure AD tenant ID
param entraIdClientId = ''        // App registration client ID
param imageTag = 'latest'
param apnsKeyId = ''              // APNs key ID for push notifications
param apnsTeamId = ''             // Apple Developer Team ID
param apnsBundleId = 'com.forexzone.ForexZoneApp'
param apnsSigningKey = ''         // Provide at deployment time: --parameters apnsSigningKey=<p8-key-contents>
