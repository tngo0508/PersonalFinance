targetScope = 'resourceGroup'

// Standalone template for local development: provisions only the Azure SQL
// server + database (Serverless Free Tier) so the app can run locally against it.

@description('Environment identifier prefix for the local development resources')
@minLength(3)
@maxLength(24)
param environmentName string = 'pf-dev'

@description('Azure region for Azure SQL resources')
param sqlLocation string = 'westus2'

@description('Administrator login for Azure SQL Database')
param sqlAdministratorLogin string = 'sqladmin'

@description('Administrator password for Azure SQL Database')
@secure()
param sqlAdministratorLoginPassword string

@description('Public IPv4 addresses of developer machines allowed through the SQL firewall')
param allowedClientIpAddresses array = []

@description('Tags to apply to all resources')
param tags object = {
  Application: 'PersonalFinance'
  Environment: environmentName
  ManagedBy: 'Bicep'
  CostTier: 'ZeroCost-Consumption'
}

var sqlServerName = 'sql-${environmentName}-${uniqueString(resourceGroup().id, sqlLocation)}'

module sqlDatabase 'modules/sql-database.bicep' = {
  name: 'sqlDatabaseLocalDevDeployment'
  params: {
    serverName: sqlServerName
    databaseName: 'PersonalFinance'
    location: sqlLocation
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
    allowedClientIpAddresses: allowedClientIpAddresses
    tags: tags
  }
}

output sqlServerName string = sqlDatabase.outputs.serverName
output sqlServerFqdn string = sqlDatabase.outputs.serverFqdn
output databaseName string = sqlDatabase.outputs.databaseName
