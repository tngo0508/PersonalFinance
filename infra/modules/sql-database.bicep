@description('Name of the Azure SQL logical server')
param serverName string

@description('Name of the Azure SQL Database')
param databaseName string = 'PersonalFinance'

@description('Azure region for the database resources')
param location string = resourceGroup().location

@description('Administrator username for the Azure SQL server')
param administratorLogin string = 'sqladmin'

@description('Administrator password for the Azure SQL server')
@secure()
param administratorLoginPassword string

@description('Additional client IPv4 addresses allowed through the server firewall (e.g. a developer machine for local development)')
param allowedClientIpAddresses array = []

@description('Tags to apply to the resources')
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

// Allow Azure services and resources (such as Azure Container Apps) to access this server
resource allowAzureIps 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Allow specific client IPs (e.g. a developer workstation running the app locally)
resource allowClientIps 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = [for (ip, i) in allowedClientIpAddresses: {
  parent: sqlServer
  name: 'AllowClientIp-${i}'
  properties: {
    startIpAddress: ip
    endIpAddress: ip
  }
}]

// Azure SQL Database — Basic tier (DTU model) for predictable ~$4.90/month cost
// This keeps total Azure costs (SQL + ACA) within a $10/month budget
// Basic tier includes 2 GB storage and 5 DTUs — sufficient for low-traffic personal finance apps
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB included in Basic tier
  }
}

output serverId string = sqlServer.id
output serverName string = sqlServer.name
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = sqlDatabase.name
