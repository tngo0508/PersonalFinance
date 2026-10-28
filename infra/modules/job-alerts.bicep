@description('Name prefix for the alert resources')
param name string

@description('Azure region for the log alert rule (the Log Analytics Workspace region)')
param location string = resourceGroup().location

@description('Resource ID of the Log Analytics Workspace the Container Apps environment logs to')
param logAnalyticsWorkspaceId string

@description('Email address the job alert is sent to')
param alertEmail string

@description('Tags to apply to the resources')
param tags object = {}

// Each scheduled job logs StartupExit.JobSucceededMessage ("Scheduled job succeeded.") only when it exits with
// success. The query returns one row per expected job with no such line in the last 26 hours, so a failure, a timeout,
// a crash and a job that never started all alert. The Job values are the container names in purge-job.bicep and
// research-refresh-job.bicep. StartupExitTests pins the text below to the C# constant. ago() takes literals only.
var missedJobsQuery = '''
datatable(Job: string) ["purge", "research-refresh"]
| join kind=leftanti (
    ContainerAppConsoleLogs_CL
    | where TimeGenerated > ago(26h)
    | where ContainerName_s in ("purge", "research-refresh")
    | where Log_s has "Scheduled job succeeded"
    | distinct Job = ContainerName_s
  ) on Job
| summarize AggregatedValue = count() by Job
'''

// Email only: SMS, voice and push are out of scope. Action Groups are global and free for email.
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${name}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'pf-jobs'
    enabled: true
    emailReceivers: [
      {
        name: 'owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// ~$0.50/month per rule plus a few cents per extra series. Split by Job, so the email names the job that missed and
// each job resolves on its own. No managed identity: the rule queries with the permissions of whoever deployed it last
// (the CI service principal); a system-assigned identity would need a role assignment CI may not be allowed to make.
resource missedJobAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: 'alert-${name}-job-missed'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    displayName: 'Scheduled job missed its success'
    description: 'A scheduled Container Apps Job (Purge or Research refresh) logged no success line in 26 hours. A Purge alert is urgent: see "A job alert fired" in docs/deployment.md.'
    severity: 2
    enabled: true
    scopes: [
      logAnalyticsWorkspaceId
    ]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    overrideQueryTimeRange: 'P2D'
    criteria: {
      allOf: [
        {
          query: missedJobsQuery
          timeAggregation: 'Total'
          metricMeasureColumn: 'AggregatedValue'
          dimensions: [
            {
              name: 'Job'
              operator: 'Include'
              values: [
                '*'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

output actionGroupName string = actionGroup.name
output alertRuleName string = missedJobAlert.name
