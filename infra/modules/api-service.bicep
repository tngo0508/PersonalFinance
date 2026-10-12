@description('Name of the API Service Container App')
param name string

@description('Azure region for the Container App')
param location string = resourceGroup().location

@description('Resource ID of the Container Apps Managed Environment')
param containerAppEnvironmentId string

@description('Container image reference for the API service (e.g. ghcr.io/<owner>/personalfinance-api:latest)')
param containerImage string

@description('Google Drive API v3 Key')
@secure()
param googleDriveApiKey string = ''

@description('Database connection string')
@secure()
param dbConnectionString string = ''

@description('CPU core allocation for the container (Consumption tier minimum: 0.25)')
param cpu string = '0.25'

@description('Memory allocation for the container (Consumption tier minimum: 0.5Gi)')
param memory string = '0.5Gi'

@description('Minimum replicas for autoscaling (set to 0 for scale-to-zero cost optimization)')
@minValue(0)
param minReplicas int = 0

@description('Maximum replicas for autoscaling')
@minValue(1)
@maxValue(10)
param maxReplicas int = 1

@description('Enable scheduled scaling: keep 1 replica warm during business hours, scale to 0 at night')
param enableScheduledScaling bool = false

@description('Business hours start time in UTC (cron minute)')
param businessHoursStartMinute int = 0

@description('Business hours start hour in UTC (0-23)')
param businessHoursStartHour int = 6

@description('Business hours end hour in UTC (0-23, scales to 0 at this hour)')
param businessHoursEndHour int = 23

@description('Expose the API publicly (external ingress) and serve the OpenAPI spec + Scalar UI. The API has no authentication, so keep this false outside of testing.')
param exposePublicly bool = false

@description('Tags to apply to the resource')
param tags object = {}

var secrets = concat(
  !empty(googleDriveApiKey) ? [
    {
      name: 'google-drive-api-key'
      value: googleDriveApiKey
    }
  ] : [],
  !empty(dbConnectionString) ? [
    {
      name: 'db-connection-string'
      value: dbConnectionString
    }
  ] : []
)

var baseEnv = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ASPNETCORE_HTTP_PORTS'
    value: '8080'
  }
  {
    name: 'ApiDocs__Enabled'
    value: string(exposePublicly)
  }
]

var googleDriveEnv = !empty(googleDriveApiKey) ? [
  {
    name: 'GoogleDrive__ApiKey'
    secretRef: 'google-drive-api-key'
  }
] : []

var dbEnv = !empty(dbConnectionString) ? [
  {
    name: 'ConnectionStrings__DefaultConnection'
    secretRef: 'db-connection-string'
  }
] : []

var containerEnvVars = concat(baseEnv, googleDriveEnv, dbEnv)

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    environmentId: containerAppEnvironmentId
    workloadProfileName: 'Consumption'
    configuration: {
      ingress: {
        external: exposePublicly // Internal-only by default; public only when explicitly exposed for testing
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: !empty(secrets) ? secrets : null
    }
    template: {
      containers: [
        {
          name: 'apiservice'
          image: containerImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: containerEnvVars
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/alive'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 3
              periodSeconds: 15
              failureThreshold: 3
              timeoutSeconds: 2
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 3
              periodSeconds: 10
              failureThreshold: 3
              timeoutSeconds: 2
            }
            {
              type: 'Startup'
              httpGet: {
                path: '/alive'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 1
              periodSeconds: 5
              failureThreshold: 30
              timeoutSeconds: 2
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: concat(
          [
            {
              name: 'http-scaling'
              http: {
                metadata: {
                  concurrentRequests: '100'
                }
              }
            }
          ],
          // Scheduled scaling: keep 1 replica during business hours, scale to 0 at night
          // Uses KEDA cron scaler: during the start-end window, scales to desiredReplicas;
          // outside the window, falls back to minReplicas (0).
          enableScheduledScaling ? [
            {
              name: 'cron-business-hours'
              custom: {
                type: 'cron'
                metadata: {
                  timezone: 'UTC'
                  start: '${string(businessHoursStartMinute)} ${string(businessHoursStartHour)} * * *'
                  end: '0 ${string(businessHoursEndHour)} * * *'
                  desiredReplicas: '1'
                }
              }
            }
          ] : []
        )
      }
    }
  }
}

output id string = apiApp.id
output name string = apiApp.name
output fqdn string = apiApp.properties.configuration.ingress.fqdn
