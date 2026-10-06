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

@description('Minimum replicas for autoscaling (0 enables scale-to-zero for zero cost)')
@minValue(0)
@maxValue(10)
param minReplicas int = 0

@description('Maximum replicas for autoscaling')
@minValue(1)
@maxValue(10)
param maxReplicas int = 1

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
        external: false // Internal-only ingress for backend API service
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
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

output id string = apiApp.id
output name string = apiApp.name
output fqdn string = apiApp.properties.configuration.ingress.fqdn
