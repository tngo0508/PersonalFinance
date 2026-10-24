@description('Name of the Research refresh Container Apps Job')
param name string

@description('Azure region for the job')
param location string = resourceGroup().location

@description('Resource ID of the Container Apps Managed Environment')
param containerAppEnvironmentId string

@description('Container image of the API service; the job runs it with --refresh-research')
param containerImage string

@description('Database connection string')
@secure()
param dbConnectionString string = ''

@description('User-Agent sent to SEC EDGAR: an application name and a contact email, as SEC fair-access rules require. Empty leaves the committed placeholder, which the job refuses to use.')
param secUserAgent string = ''

@description('When the refresh runs (UTC cron). Off-peak, after SEC republishes its data at about 3:00 a.m. ET.')
param cronExpression string = '0 10 * * *'

@description('CPU core allocation (Consumption tier minimum: 0.25)')
param cpu string = '0.25'

@description('Memory allocation (Consumption tier minimum: 0.5Gi)')
param memory string = '0.5Gi'

@description('Tags to apply to the resource')
param tags object = {}

// Daily Research refresh: rebuilds the Research page's public market snapshots (SEC EDGAR) and exits. The ApiService
// scales to zero, so the refresh runs as a scheduled job instead of inside it. Billed only while it runs (minutes a
// day), within the free grant. A failed run keeps the previous snapshots.
resource researchRefreshJob 'Microsoft.App/jobs@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    environmentId: containerAppEnvironmentId
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Schedule'
      scheduleTriggerConfig: {
        cronExpression: cronExpression
        parallelism: 1
        replicaCompletionCount: 1
      }
      // About 60 SEC requests, throttled under SEC's 10 a second; a failed run is retried once.
      replicaTimeout: 1800
      replicaRetryLimit: 1
      secrets: !empty(dbConnectionString) ? [
        {
          name: 'db-connection-string'
          value: dbConnectionString
        }
      ] : null
    }
    template: {
      containers: [
        {
          name: 'research-refresh'
          image: containerImage
          args: [
            '--refresh-research'
          ]
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
          ], !empty(secUserAgent) ? [
            {
              name: 'Research__Sec__UserAgent'
              value: secUserAgent
            }
          ] : [], !empty(dbConnectionString) ? [
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'db-connection-string'
            }
          ] : [])
        }
      ]
    }
  }
}

output name string = researchRefreshJob.name
