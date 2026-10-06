targetScope = 'resourceGroup'

@description('Environment identifier prefix for all deployed Azure resources (e.g. personalfinance-prod)')
@minLength(3)
@maxLength(24)
param environmentName string = 'pf-prod'

@description('Azure region for the deployed resources')
param location string = resourceGroup().location

@description('Azure region for Azure SQL resources. This can differ from the Container Apps region when SQL provisioning is restricted.')
param sqlLocation string = 'westus2'

@description('Container image reference for PersonalFinance.ApiService from GHCR or container registry')
param apiImage string = 'ghcr.io/tngo0508/personalfinance-api:latest'

@description('Container image reference for PersonalFinance.Web from GHCR or container registry')
param webImage string = 'ghcr.io/tngo0508/personalfinance-web:latest'

@description('Google OAuth 2.0 Client ID for external sign-in')
@secure()
param googleClientId string = ''

@description('Google OAuth 2.0 Client Secret for external sign-in')
@secure()
param googleClientSecret string = ''

@description('Brevo REST API Key for transactional emails')
@secure()
param brevoApiKey string = ''

@description('Brevo verified sender email address')
param brevoSenderEmail string = 'tngo0508@gmail.com'

@description('Brevo sender display name')
param brevoSenderName string = 'PersonalFinance'

@description('Google Drive API v3 Key')
@secure()
param googleDriveApiKey string = ''

@description('Whether to provision Azure SQL Database Serverless Free Tier ($0.00 with 100k vCore-s + 32GB free lifetime)')
param deploySqlDatabase bool = true

@description('Administrator login for Azure SQL Database (if deployed)')
param sqlAdministratorLogin string = 'sqladmin'

@description('Administrator password for Azure SQL Database (if deployed)')
@secure()
param sqlAdministratorLoginPassword string = ''

@description('Optional database connection string override (used when deploySqlDatabase is false and connecting to existing/external database or mounted storage)')
@secure()
param customConnectionString string = ''

@description('Optional custom domain name for the Web application (e.g. finance.mydomain.com)')
param customDomainName string = ''

@description('Optional certificate ID for custom domain')
param certificateId string = ''

@description('Tags to apply to all resources')
param tags object = {
  Application: 'PersonalFinance'
  Environment: environmentName
  ManagedBy: 'Bicep'
  CostTier: 'ZeroCost-Consumption'
}

var locationSuffix = uniqueString(resourceGroup().id, location)
var logAnalyticsWorkspaceName = 'law-${environmentName}-${locationSuffix}'
var containerAppEnvironmentName = 'cae-${environmentName}-${locationSuffix}'
var apiAppName = 'api-${environmentName}-${locationSuffix}'
var webAppName = 'web-${environmentName}-${locationSuffix}'
// Include the SQL region in the deterministic name. Azure SQL server names are
// location-bound, and a failed deployment can leave a name reserved in the
// original region even when provisioning did not complete.
var sqlServerName = 'sql-${environmentName}-${uniqueString(resourceGroup().id, sqlLocation)}'

// 1. Free 5GB/month Log Analytics Workspace
module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalyticsDeployment'
  params: {
    name: logAnalyticsWorkspaceName
    location: location
    tags: tags
  }
}

// 2. Azure Container Apps Managed Environment (Consumption Profile)
module containerEnv 'modules/container-env.bicep' = {
  name: 'containerEnvDeployment'
  params: {
    name: containerAppEnvironmentName
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
    tags: tags
  }
}

// 3. Optional Azure SQL Database Lifetime Free Tier
module sqlDatabase 'modules/sql-database.bicep' = if (deploySqlDatabase) {
  name: 'sqlDatabaseDeployment'
  params: {
    serverName: sqlServerName
    databaseName: 'PersonalFinance'
    location: sqlLocation
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
    tags: tags
  }
}

var effectiveConnectionString = deploySqlDatabase
  ? 'Server=tcp:${sqlDatabase.?outputs.serverFqdn ?? ''},1433;Initial Catalog=${sqlDatabase.?outputs.databaseName ?? ''};Persist Security Info=False;User ID=${sqlAdministratorLogin};Password=${sqlAdministratorLoginPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  : customConnectionString

// 4. API Service Container App (Internal ingress, scale-to-zero)
module apiService 'modules/api-service.bicep' = {
  name: 'apiServiceDeployment'
  params: {
    name: apiAppName
    location: location
    containerAppEnvironmentId: containerEnv.outputs.id
    containerImage: apiImage
    googleDriveApiKey: googleDriveApiKey
    dbConnectionString: effectiveConnectionString
    tags: tags
  }
}

// 5. Web App Container App (External ingress, scale-to-zero)
module webService 'modules/web-service.bicep' = {
  name: 'webServiceDeployment'
  params: {
    name: webAppName
    location: location
    containerAppEnvironmentId: containerEnv.outputs.id
    containerImage: webImage
    apiServiceName: apiService.outputs.name
    apiServiceFqdn: apiService.outputs.fqdn
    googleClientId: googleClientId
    googleClientSecret: googleClientSecret
    brevoApiKey: brevoApiKey
    brevoSenderEmail: brevoSenderEmail
    brevoSenderName: brevoSenderName
    dbConnectionString: effectiveConnectionString
    customDomainName: customDomainName
    certificateId: certificateId
    tags: tags
  }
}

output webUrl string = webService.outputs.url
output webFqdn string = webService.outputs.fqdn
output apiFqdn string = apiService.outputs.fqdn
output containerAppEnvironmentName string = containerEnv.outputs.name
output logAnalyticsWorkspaceName string = logAnalytics.outputs.name
output sqlServerFqdn string = deploySqlDatabase ? (sqlDatabase.?outputs.serverFqdn ?? '') : ''
