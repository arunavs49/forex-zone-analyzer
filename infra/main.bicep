targetScope = 'subscription'

@description('Azure region for all resources')
param location string = 'eastus2'

@description('Base name for resource naming')
param baseName string = 'forex-mcp'

@description('OANDA API token (stored in Key Vault)')
@secure()
param oandaApiToken string

@description('OANDA connection type: FxPractice or FxTrade')
@allowed(['FxPractice', 'FxTrade'])
param oandaConnectionType string = 'FxPractice'

@description('Entra ID tenant ID for MCP server authentication')
param entraIdTenantId string

@description('Entra ID client/application ID for MCP server authentication')
param entraIdClientId string

@description('Container image tag')
param imageTag string = 'latest'

@description('Deploy the Container App (set to false for initial infra-only deployment)')
param deployApp bool = true

@description('Deploy the Worker Container App')
param deployWorker bool = true

@description('Notification email address for zone alerts')
param notificationEmail string = ''

var resourceGroupName = 'rg-${baseName}'

resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: resourceGroupName
  location: location
}

module resources 'modules/resources.bicep' = {
  scope: rg
  name: 'resources-deployment'
  params: {
    location: location
    baseName: baseName
    oandaApiToken: oandaApiToken
    oandaConnectionType: oandaConnectionType
    entraIdTenantId: entraIdTenantId
    entraIdClientId: entraIdClientId
    imageTag: imageTag
    deployApp: deployApp
    deployWorker: deployWorker
    notificationEmail: notificationEmail
  }
}

output resourceGroupName string = rg.name
output containerAppFqdn string = deployApp ? resources.outputs.containerAppFqdn : 'not-deployed'
output containerRegistryLoginServer string = resources.outputs.containerRegistryLoginServer
output keyVaultName string = resources.outputs.keyVaultName
output mcpEndpoint string = deployApp ? 'https://${resources.outputs.containerAppFqdn}/mcp' : 'not-deployed'
output storageAccountName string = resources.outputs.storageAccountName
