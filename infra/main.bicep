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

@description('OpenAI API key for the AI Assistant feature')
@secure()
param openAiApiKey string = ''

@description('Whether to provision Azure SQL Database Basic tier (~$4.90/month, 2 GB storage, 5 DTUs)')
param deploySqlDatabase bool = true

@description('Email of the account granted the Admin role at startup. The account must exist and have a confirmed email.')
param rbacAdminEmail string = 'tngo0508@gmail.com'

@description('Shared secret between Web and ApiService (X-Internal-Key). Without it ApiService attaches no roles in production.')
@secure()
param internalApiKey string = ''

@description('Number of days of log retention in the Log Analytics Workspace (30 days included in free tier)')
@minValue(30)
@maxValue(730)
param logRetentionInDays int = 30

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

@description('Expose the API service publicly with the Scalar UI enabled. The API has no authentication; use for testing only.')
param exposeApiPublicly bool = false

@description('Enable scheduled scaling: keep 1 replica warm during business hours, scale to 0 at night')
param enableScheduledScaling bool = false

@description('Business hours start hour (0-23, default 06:00), in businessHoursTimeZone')
param businessHoursStartHour int = 6

@description('Business hours start minute (0-59, default 00), in businessHoursTimeZone')
param businessHoursStartMinute int = 0

@description('Business hours end hour (0-23, scales to 0 at this hour, default 23:00), in businessHoursTimeZone')
param businessHoursEndHour int = 23

@description('IANA time zone the business hours are in, e.g. America/Los_Angeles. The cron scaler follows its daylight saving changes.')
param businessHoursTimeZone string = 'UTC'

@description('When the daily Purge job runs (UTC cron). Default 15:00 UTC, inside business hours, when the database is usually awake.')
param purgeJobCron string = '0 15 * * *'

@description('When the daily Research refresh job runs (UTC cron). Default 10:00 UTC: off-peak, after SEC republishes its data.')
param researchRefreshJobCron string = '0 10 * * *'

@description('User-Agent the Research refresh job sends to SEC EDGAR: an application name and a contact email (SEC fair-access rules)')
param secUserAgent string = ''

// =============================================================================
// BUDGET ESTIMATE (~$10/month total)
// =============================================================================
// Azure SQL Database (Basic tier):       ~$4.90/month  (2 GB storage, 5 DTUs)
// ACA Consumption (scheduled scaling):   ~$5-18/month  (1 replica per app during business hours, 0 at night;
//                                                       production keeps 08:00-22:00 Pacific warm)
// ACA Managed Environment:               ~$1.75/month  (Consumption tier base cost)
// Log Analytics:                         ~$0/month     (5 GB/month free ingestion, 30-day retention)
// ACA Job (daily Purge):                 ~$0/month     (seconds a day, within the Consumption free grant)
// ACA Job (daily Research refresh):      ~$0/month     (minutes a day, within the Consumption free grant)
// ---------------------------------------------------------------------------
// Total estimated cost:                  ~$10/month
// =============================================================================
// NOTE: To stay within budget, enable scheduled scaling when deploying:
//   azd deploy --param enableScheduledScaling=true
// Without scheduled scaling, ACA replicas running 24/7 will exceed the budget.
// =============================================================================

@description('Tags to apply to all resources')
param tags object = {
  Application: 'PersonalFinance'
  Environment: environmentName
  ManagedBy: 'Bicep'
  CostTier: 'Budget10USD-Monthly'
}

var locationSuffix = uniqueString(resourceGroup().id, location)
var logAnalyticsWorkspaceName = 'law-${environmentName}-${locationSuffix}'
var containerAppEnvironmentName = 'cae-${environmentName}-${locationSuffix}'
var apiAppName = 'api-${environmentName}-${locationSuffix}'
var webAppName = 'web-${environmentName}-${locationSuffix}'
var purgeJobName = 'purge-${environmentName}-${locationSuffix}'
var researchRefreshJobName = 'research-${environmentName}-${locationSuffix}'
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
    retentionInDays: logRetentionInDays
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

// 3. Optional Azure SQL Database Basic tier (~$4.90/month)
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

// 4. API Service Container App (Internal ingress unless exposeApiPublicly, scale-to-zero)
module apiService 'modules/api-service.bicep' = {
  name: 'apiServiceDeployment'
  params: {
    name: apiAppName
    location: location
    containerAppEnvironmentId: containerEnv.outputs.id
    containerImage: apiImage
    googleDriveApiKey: googleDriveApiKey
    dbConnectionString: effectiveConnectionString
    openAiApiKey: openAiApiKey
    internalApiKey: internalApiKey
    exposePublicly: exposeApiPublicly
    enableScheduledScaling: enableScheduledScaling
    businessHoursStartHour: businessHoursStartHour
    businessHoursStartMinute: businessHoursStartMinute
    businessHoursEndHour: businessHoursEndHour
    businessHoursTimeZone: businessHoursTimeZone
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
    rbacAdminEmail: rbacAdminEmail
    internalApiKey: internalApiKey
    enableScheduledScaling: enableScheduledScaling
    businessHoursStartHour: businessHoursStartHour
    businessHoursStartMinute: businessHoursStartMinute
    businessHoursEndHour: businessHoursEndHour
    businessHoursTimeZone: businessHoursTimeZone
    customDomainName: customDomainName
    certificateId: certificateId
    tags: tags
  }
}

// 6. Daily Purge job (ADR 0005): runs the API image with --purge-only, since the API itself scales to zero
module purgeJob 'modules/purge-job.bicep' = {
  name: 'purgeJobDeployment'
  params: {
    name: purgeJobName
    location: location
    containerAppEnvironmentId: containerEnv.outputs.id
    containerImage: apiImage
    dbConnectionString: effectiveConnectionString
    cronExpression: purgeJobCron
    tags: tags
  }
}

// 7. Daily Research refresh job: runs the API image with --refresh-research to rebuild the Research snapshots
module researchRefreshJob 'modules/research-refresh-job.bicep' = {
  name: 'researchRefreshJobDeployment'
  params: {
    name: researchRefreshJobName
    location: location
    containerAppEnvironmentId: containerEnv.outputs.id
    containerImage: apiImage
    dbConnectionString: effectiveConnectionString
    secUserAgent: secUserAgent
    cronExpression: researchRefreshJobCron
    tags: tags
  }
}

output webUrl string = webService.outputs.url
output webFqdn string = webService.outputs.fqdn
output apiFqdn string = apiService.outputs.fqdn
output containerAppEnvironmentName string = containerEnv.outputs.name
output logAnalyticsWorkspaceName string = logAnalytics.outputs.name
output sqlServerFqdn string = deploySqlDatabase ? (sqlDatabase.?outputs.serverFqdn ?? '') : ''
