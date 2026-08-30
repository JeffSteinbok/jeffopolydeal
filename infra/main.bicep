// ──────────────────────────────────────────────────────────────
// Jeffopoly Deal — Azure Infrastructure
//
// Deploys all resources needed for the app:
//   - App Service Plan (B1 Windows)
//   - Web App (.NET 10, WebSockets for SignalR)
//   - Log Analytics Workspace
//   - Application Insights (OpenTelemetry)
//   - Game Analytics Workbook
//
// Usage:
//   az deployment group create \
//     --resource-group jeffopolydeal \
//     --template-file infra/main.bicep
// ──────────────────────────────────────────────────────────────

@description('Azure region for primary resources (App Service)')
param appLocation string = 'westus3'

@description('Azure region for monitoring resources (App Insights + Log Analytics)')
param monitoringLocation string = 'westus2'

@description('App Service Plan SKU')
param planSku string = 'B1'

// ── App Service Plan ─────────────────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-jeffopolydeal'
  location: appLocation
  kind: 'app'
  sku: {
    name: planSku
    tier: planSku == 'B1' ? 'Basic' : 'Standard'
    capacity: 1
  }
}

// ── Web App ──────────────────────────────────────────────────

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'jeffopolydeal'
  location: appLocation
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      webSocketsEnabled: true
      http20Enabled: true
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: true
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'APNS__TOPIC'
          value: 'net.steinbok.jeffopolydeal'
        }
      ]
    }
  }
}

// ── Log Analytics Workspace ──────────────────────────────────

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'jeffopolydeal-logs'
  location: monitoringLocation
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ── Application Insights ─────────────────────────────────────

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'jeffopolydeal-appinsights'
  location: monitoringLocation
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
    DisableIpMasking: true
  }
}

// ── Game Analytics Workbook ──────────────────────────────────

resource workbook 'Microsoft.Insights/workbooks@2022-04-01' = {
  name: guid('jeffopolydeal-game-analytics')
  location: monitoringLocation
  kind: 'shared'
  properties: {
    displayName: 'Jeffopoly Deal - Game Analytics'
    category: 'workbook'
    sourceId: appInsights.id
    version: '1.0'
    serializedData: loadTextContent('workbook.json')
  }
}

// ── Outputs ──────────────────────────────────────────────────

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output workbookId string = workbook.id
