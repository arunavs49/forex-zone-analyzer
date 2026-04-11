using 'main.bicep'

param location = 'eastus2'
param baseName = 'forex-mcp'
param oandaApiToken = ''          // Provide at deployment time: --parameters oandaApiToken=<token>
param oandaConnectionType = 'FxPractice'
param entraIdTenantId = ''        // Your Azure AD tenant ID
param entraIdClientId = ''        // App registration client ID
param imageTag = 'latest'
