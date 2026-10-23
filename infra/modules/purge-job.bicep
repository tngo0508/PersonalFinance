@description('Name of the Purge Container Apps Job')
param name string

@description('Azure region for the job')
param location string = resourceGroup().location

@description('Resource ID of the Container Apps Managed Environment')
param containerAppEnvironmentId string

@description('Container image of the API service; the job runs it with --purge-only')
param containerImage string

@description('Database connection string')
@secure()
param dbConnectionString string = ''

@description('When the Purge runs (UTC cron). Pick a time the database is usually awake anyway.')
param cronExpression string = '0 15 * * *'

@description('CPU core allocation (Consumption tier minimum: 0.25)')
param cpu string = '0.25'

@description('Memory allocation (Consumption tier minimum: 0.5Gi)')
param memory string = '0.5Gi'

@description('Tags to apply to the resource')
param tags object = {}

// Daily Purge (ADR 0005): erases Accounts whose 30-day Grace Period has ended. The ApiService scales to zero, so the
// Purge runs as a scheduled job instead of inside it. Billed only while it runs (seconds a day), within the free grant.
resource purgeJob 'Microsoft.App/jobs@2024-03-01' = {
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
      // One run per day is enough; a failed run is retried once, and the next day's run picks up anything left.
      replicaTimeout: 900
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
          name: 'purge'
          image: containerImage
          args: [
            '--purge-only'
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
          ], !empty(dbConnectionString) ? [
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

output name string = purgeJob.name
