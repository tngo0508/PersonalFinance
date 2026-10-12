@description('Name of the Web Service Container App')
param name string

@description('Azure region for the Container App')
param location string = resourceGroup().location

@description('Resource ID of the Container Apps Managed Environment')
param containerAppEnvironmentId string

@description('Container image reference for the Web service (e.g. ghcr.io/<owner>/personalfinance-web:latest)')
param containerImage string

@description('Name of the internal API service Container App')
param apiServiceName string

@description('Fully Qualified Domain Name of the internal API service Container App')
param apiServiceFqdn string

@description('Google OAuth 2.0 Client ID')
@secure()
param googleClientId string = ''

@description('Google OAuth 2.0 Client Secret')
@secure()
param googleClientSecret string = ''

@description('Brevo REST API Key for transactional emails')
@secure()
param brevoApiKey string = ''

@description('Brevo verified sender email address')
param brevoSenderEmail string = 'tngo0508@gmail.com'

@description('Brevo sender display name')
param brevoSenderName string = 'PersonalFinance'

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

@description('Optional custom domain name (e.g. app.mydomain.com)')
param customDomainName string = ''

@description('Optional Managed Certificate ID for custom domain')
param certificateId string = ''

@description('Tags to apply to the resource')
param tags object = {}

var secrets = concat(
  !empty(googleClientId) ? [
    {
      name: 'google-client-id'
      value: googleClientId
    }
  ] : [],
  !empty(googleClientSecret) ? [
    {
      name: 'google-client-secret'
      value: googleClientSecret
    }
  ] : [],
  !empty(brevoApiKey) ? [
    {
      name: 'brevo-api-key'
      value: brevoApiKey
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
    name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
    value: 'true'
  }
  {
    name: 'ApiSettings__BaseUrl'
    value: 'https://${apiServiceFqdn}'
  }
  {
    name: 'services__apiservice__http__0'
    value: 'http://${apiServiceName}'
  }
  {
    name: 'Brevo__SenderEmail'
    value: brevoSenderEmail
  }
  {
    name: 'Brevo__SenderName'
    value: brevoSenderName
  }
]

var googleClientIdEnv = !empty(googleClientId) ? [
  {
    name: 'Authentication__Google__ClientId'
    secretRef: 'google-client-id'
  }
] : []

var googleClientSecretEnv = !empty(googleClientSecret) ? [
  {
    name: 'Authentication__Google__ClientSecret'
    secretRef: 'google-client-secret'
  }
] : []

var brevoEnv = !empty(brevoApiKey) ? [
  {
    name: 'Brevo__ApiKey'
    secretRef: 'brevo-api-key'
  }
] : []

var dbEnv = !empty(dbConnectionString) ? [
  {
    name: 'ConnectionStrings__DefaultConnection'
    secretRef: 'db-connection-string'
  }
] : []

var containerEnvVars = concat(baseEnv, googleClientIdEnv, googleClientSecretEnv, brevoEnv, dbEnv)

resource webApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    environmentId: containerAppEnvironmentId
    workloadProfileName: 'Consumption'
    configuration: {
      ingress: {
        external: true // Public HTTPS ingress
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        customDomains: !empty(customDomainName) ? [
          {
            name: customDomainName
            certificateId: !empty(certificateId) ? certificateId : null
            bindingType: !empty(certificateId) ? 'SniEnabled' : 'Disabled'
          }
        ] : null
      }
      secrets: !empty(secrets) ? secrets : null
    }
    template: {
      containers: [
        {
          name: 'web'
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

output id string = webApp.id
output name string = webApp.name
output fqdn string = webApp.properties.configuration.ingress.fqdn
output url string = 'https://${webApp.properties.configuration.ingress.fqdn}'
