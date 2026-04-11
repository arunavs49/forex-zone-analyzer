@description('Azure region for all resources')
param location string

@description('Base name for resource naming')
param baseName string

@description('OANDA API token')
@secure()
param oandaApiToken string

@description('OANDA connection type')
param oandaConnectionType string

@description('Entra ID tenant ID')
param entraIdTenantId string

@description('Entra ID client ID')
param entraIdClientId string

@description('Container image tag')
param imageTag string

@description('Deploy the Container App')
param deployApp bool

@description('Deploy the Worker Container App')
param deployWorker bool

@description('Notification email address')
param notificationEmail string

@description('APNs key ID for push notifications')
param apnsKeyId string

@description('APNs team ID for push notifications')
param apnsTeamId string

@description('APNs bundle ID for push notifications')
param apnsBundleId string

@description('APNs signing key (p8 file contents)')
@secure()
param apnsSigningKey string

var uniqueSuffix = uniqueString(resourceGroup().id)
var acrName = replace('acr${baseName}${uniqueSuffix}', '-', '')
var keyVaultName = 'kv-${baseName}-${take(uniqueSuffix, 6)}'
var managedIdentityName = 'id-${baseName}'
var logAnalyticsName = 'log-${baseName}'
var containerAppEnvName = 'cae-${baseName}'
var containerAppName = 'ca-${baseName}'
var workerAppName = 'ca-${baseName}-worker'
var storageAccountName = replace('st${baseName}${take(uniqueSuffix, 8)}', '-', '')
var communicationServiceName = 'acs-${baseName}'
var emailServiceName = 'acs-email-${baseName}'
var notificationHubNamespaceName = 'nhns-${baseName}'
var notificationHubName = 'nh-${baseName}'

// User-assigned managed identity for the Container App
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' = {
  name: managedIdentityName
  location: location
}

// Log Analytics workspace for Container Apps
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Azure Container Registry
resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// ACR Pull role assignment for managed identity
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, managedIdentity.id, '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Azure Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }
}

// Key Vault Secrets User role for managed identity
resource kvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, managedIdentity.id, '4633458b-17de-408a-b874-0445c86b69e6')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Store OANDA token as a secret
resource oandaSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'oanda-api-token'
  properties: {
    value: oandaApiToken
  }
}

// Storage Account for zone persistence
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

// Storage Table Data Contributor role for managed identity
resource storageTableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, managedIdentity.id, '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3') // Storage Table Data Contributor
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Azure Communication Services
resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationServiceName
  location: 'global'
  properties: {
    dataLocation: 'United States'
    linkedDomains: [
      emailDomain.id
    ]
  }
}

// Email Communication Services
resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: emailServiceName
  location: 'global'
  properties: {
    dataLocation: 'United States'
  }
}

// Azure-managed email domain (free *.azurecomm.net)
resource emailDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

// Store ACS connection string in Key Vault
resource acsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'acs-connection-string'
  properties: {
    value: communicationService.listKeys().primaryConnectionString
  }
}

// Azure Notification Hub Namespace (Free tier)
resource notificationHubNamespace 'Microsoft.NotificationHubs/namespaces@2023-10-01-preview' = {
  name: notificationHubNamespaceName
  location: location
  sku: {
    name: 'Free'
  }
  properties: {
    zoneRedundancy: 'Disabled'
  }
}

// Azure Notification Hub with APNs token auth
resource notificationHub 'Microsoft.NotificationHubs/namespaces/notificationHubs@2023-10-01-preview' = {
  parent: notificationHubNamespace
  name: notificationHubName
  location: location
  properties: {
    apnsCredential: !empty(apnsSigningKey) ? {
      properties: {
        appName: apnsBundleId
        appId: apnsBundleId
        keyId: apnsKeyId
        token: apnsSigningKey
        endpoint: 'https://api.sandbox.push.apple.com:443/3/device'
      }
    } : null
  }
}

// Store NH connection string in Key Vault (DefaultFullSharedAccessSignature)
resource nhConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'nh-connection-string'
  properties: {
    value: listKeys(notificationHub.id, '2023-10-01-preview').value[0].value
  }
}

// Store NH listen-only connection string for the iOS app
resource nhListenConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'nh-listen-connection-string'
  properties: {
    value: listKeys(notificationHub.id, '2023-10-01-preview').value[1].value
  }
}

// Container Apps Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Container App (conditional - skip on initial infra-only deployment)
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = if (deployApp) {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: managedIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'mcp-server'
          image: '${acr.properties.loginServer}/forex-mcp-server:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'KeyVault__Uri'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'KeyVault__OandaTokenSecretName'
              value: 'oanda-api-token'
            }
            {
              name: 'Oanda__ConnectionType'
              value: oandaConnectionType
            }
            {
              name: 'AzureAd__Instance'
              value: 'https://login.microsoftonline.com/'
            }
            {
              name: 'AzureAd__TenantId'
              value: entraIdTenantId
            }
            {
              name: 'AzureAd__ClientId'
              value: entraIdClientId
            }
            {
              name: 'AzureAd__Audience'
              value: 'api://${entraIdClientId}'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: managedIdentity.properties.clientId
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
}

// Worker Container App (no ingress — background processing only)
resource workerApp 'Microsoft.App/containerApps@2024-03-01' = if (deployWorker) {
  name: workerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      secrets: [
        {
          name: 'acs-connection-string'
          value: communicationService.listKeys().primaryConnectionString
        }
        {
          name: 'nh-connection-string'
          value: listKeys(notificationHub.id, '2023-10-01-preview').value[0].value
        }
      ]
      registries: [
        {
          server: acr.properties.loginServer
          identity: managedIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: '${acr.properties.loginServer}/forex-worker:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'KeyVault__Uri'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'KeyVault__OandaTokenSecretName'
              value: 'oanda-api-token'
            }
            {
              name: 'Oanda__ConnectionType'
              value: oandaConnectionType
            }
            {
              name: 'Storage__AccountName'
              value: storageAccount.name
            }
            {
              name: 'Storage__TableName'
              value: 'zones'
            }
            {
              name: 'Notification__EmailTo'
              value: notificationEmail
            }
            {
              name: 'Notification__EmailFrom'
              value: 'DoNotReply@${emailDomain.properties.mailFromSenderDomain}'
            }
            {
              name: 'Notification__AcsConnectionString'
              secretRef: 'acs-connection-string'
            }
            {
              name: 'Notification__NotificationHubConnectionString'
              secretRef: 'nh-connection-string'
            }
            {
              name: 'Notification__NotificationHubName'
              value: notificationHub.name
            }
            {
              name: 'MonitorSettings__Instruments__0'
              value: 'EUR_USD'
            }
            {
              name: 'MonitorSettings__Instruments__1'
              value: 'GBP_USD'
            }
            {
              name: 'MonitorSettings__Instruments__2'
              value: 'USD_JPY'
            }
            {
              name: 'MonitorSettings__Instruments__3'
              value: 'AUD_USD'
            }
            {
              name: 'MonitorSettings__ZoneGranularity'
              value: 'M15'
            }
            {
              name: 'MonitorSettings__TrendGranularity'
              value: 'H1'
            }
            {
              name: 'MonitorSettings__PollIntervalMinutes'
              value: '15'
            }
            {
              name: 'MonitorSettings__CandleCacheSize'
              value: '2000'
            }
            {
              name: 'MonitorSettings__CandleOverlapCount'
              value: '5'
            }
            {
              name: 'ZoneConfiguration__MinBaseLength'
              value: '1'
            }
            {
              name: 'ZoneConfiguration__MaxBaseLength'
              value: '6'
            }
            {
              name: 'ZoneConfiguration__MinLegInToBaseRangeRatio'
              value: '1.0'
            }
            {
              name: 'ZoneConfiguration__MinLegOutToBaseRangeRatio'
              value: '1.0'
            }
            {
              name: 'TrendConfiguration__SwingLookback'
              value: '3'
            }
            {
              name: 'TrendConfiguration__TrendCandleCount'
              value: '60'
            }
            {
              name: 'TrendConfiguration__MinSwingPoints'
              value: '2'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: managedIdentity.properties.clientId
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output containerAppFqdn string = deployApp ? containerApp.properties.configuration.ingress.fqdn : 'not-deployed'
output containerRegistryLoginServer string = acr.properties.loginServer
output keyVaultName string = keyVault.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output storageAccountName string = storageAccount.name
output notificationHubName string = notificationHub.name
