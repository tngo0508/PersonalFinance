@description('Name of the Log Analytics Workspace')
param name string

@description('Azure region for the Log Analytics Workspace')
param location string = resourceGroup().location

@description('Daily ingestion volume cap in GB. Default is -1 (unlimited / no daily cap within the 5 GB/month free tier). Specify a positive integer (e.g. 1) to enforce a hard daily ingestion cap.')
param dailyQuotaGb int = -1

@description('Number of days of log retention. 30 days is included in the free tier.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Tags to apply to the resource')
param tags object = {}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    workspaceCapping: dailyQuotaGb > 0 ? {
      dailyQuotaGb: dailyQuotaGb
    } : null
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output id string = logAnalyticsWorkspace.id
output name string = logAnalyticsWorkspace.name
output customerId string = logAnalyticsWorkspace.properties.customerId
