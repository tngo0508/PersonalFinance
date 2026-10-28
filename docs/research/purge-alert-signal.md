# Research: which Azure signal shows a Container Apps Job missed its success, and what it costs

**Ticket:** [#498](https://github.com/tngo0508/PersonalFinance/issues/498) (part of map #497, feature #150 "Alert when
the daily Purge job fails"; unblocks #499)  
**Date:** 2026-10-27  
**Scope:** the daily Purge job (`infra/modules/purge-job.bicep`, cron `0 15 * * *` UTC) and the daily Research refresh
job (`infra/modules/research-refresh-job.bicep`, cron `0 10 * * *` UTC). Both are `Microsoft.App/jobs@2024-03-01`,
`triggerType: 'Schedule'`, Consumption profile, `replicaRetryLimit: 1`, in an environment whose
`appLogsConfiguration.destination` is `'log-analytics'` (`infra/modules/container-env.bicep`) pointing at a
`PerGB2018` workspace (`infra/modules/log-analytics.bicep`), region `eastus` (`infra/main.parameters.json`).

Every fact below cites the Microsoft page (learn.microsoft.com, azure.microsoft.com) or first-party API that owns it,
read on 2026-10-27. Prices come from the Azure Retail Prices API (`prices.azure.com`), the machine-readable source behind
the pricing page, because the pricing page renders its dollar figures with script and shows `$-` to a plain fetch.
Anything not stated by a primary source is marked **Unconfirmed**.

## Short answers

| Question | Answer |
|---|---|
| 1. Metrics | Yes. `Microsoft.App/jobs` emits **`Executions`** ("Job Executions"), unit Count, dimensions `state`, `jobName`, `executionName`, grain PT1M. A metric alert window is at most **one day**, so it cannot look back 26 h. The values of `state` and whether the rule fires when **no** execution is emitted at all are **not documented**. |
| 2. Logs | The environment writes **`ContainerAppSystemLogs_CL`** and **`ContainerAppConsoleLogs_CL`** by default with the `log-analytics` destination. No Microsoft page lists the `Reason` value of a successful job execution, so the reliable success marker is **our own console line**, written only on exit code 0. A `datatable` of expected jobs plus a `leftanti` join fires when a job failed **and** when it never started. |
| 3. Price | Metric alert: first 10 time series free, then $0.10 each a month. Log search alert: $0.50 (15 min), $1.00 (10 min), $1.50 (5 min), $3.00 (1 min) per rule a month; there is no meter for slower frequencies. Email: first 1,000 a month free, then $0.00002 each. |
| 4. Bicep | `Microsoft.Insights/actionGroups@2023-01-01` (location `'global'`), `Microsoft.Insights/scheduledQueryRules@2023-12-01` or `@2026-03-01`, `Microsoft.Insights/metricAlerts@2018-03-01` or `@2026-01-01`. Gotchas: a log alert with frequency over 12 h cannot be stateful; the query time range caps at 2 days; `ago()` takes literals only; a rule with no identity runs with the last editor's permissions. |
| **Recommendation** | **One log search alert rule** on the workspace, hourly, over `ContainerAppConsoleLogs_CL`, covering both jobs, stateful (auto-resolves), emailing one Action Group. **About $0.50 a month** (see §5), well inside $2. |

## 1. Metrics: `Microsoft.App/jobs`

The supported-metrics reference lists, under category Basic
(<https://learn.microsoft.com/en-us/azure/azure-monitor/reference/supported-metrics/microsoft-app-jobs-metrics>):

| Metric (REST name) | Unit | Aggregations | Dimensions | Grain |
|---|---|---|---|---|
| Job Executions (`Executions`): "Executions run by the Container Apps Job" | Count | Average, Total, Maximum, Minimum | `state`, `jobName`, `executionName` | PT1M |
| Total Job Execution Restart Count (`RestartCount`) | Count | same | same | PT1M |
| `RequestedBytes`, `RequestedCores`, `RxBytes`, `TxBytes`, `UsageBytes`, `UsageNanoCores` | resource usage | same | same | PT1M |

There is no separate started/succeeded/failed metric; outcome would come from the `state` dimension.

- **Values of `state`: Unconfirmed.** No metrics page lists them. The executions REST API's `JobExecutionRunningState`
  enum is `Running, Processing, Stopped, Degraded, Failed, Unknown, Succeeded`
  (<https://learn.microsoft.com/en-us/rest/api/resource-manager/containerapps/jobs-executions/list>), and it is likely,
  but not documented, that the metric dimension uses the same strings. Check before relying on it:
  `az monitor metrics list --resource <job-id> --metric Executions --aggregation Total --interval PT1H --filter "state eq '*'"`.
- **Metric costs nothing to collect:** "There's no charge for standard metrics collected from Azure resources"
  (<https://learn.microsoft.com/en-us/azure/azure-monitor/fundamentals/cost-usage>).
- **Window and frequency limits.** The ARM sample for metric alerts describes `windowSize` as "Must be between five
  minutes and one day" (<https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-metric-logs>, which says
  those rules "have all characteristics and functionalities of metric alerts"). So the longest look-back is **24 h**
  (`PT24H`, the portal's "Lookback period"), so a 26-hour window is not possible. The largest allowed
  `evaluationFrequency` is not stated on any page read (**Unconfirmed**; the CLI and REST examples use `PT1M`). The
  template reference only says "ISO 8601 duration" for both
  (<https://learn.microsoft.com/en-us/azure/templates/microsoft.insights/metricalerts>).
- **"Succeeded total = 0 over 24 h" when nothing ran: Unconfirmed, and likely not to fire.** A metric alert evaluates
  the time series that exist. The troubleshooting guide treats a metric that "isn't emitted continuously" as a **sparse
  metric** and warns the rule can miss its first evaluation after a gap over 24 hours
  (<https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-troubleshoot-metric>). No page says that an
  absent `state = Succeeded` series is evaluated as 0. A job that never starts emits no `Executions` point, so a metric
  rule may stay silent in exactly the case #150 most needs.
- **Fit for our schedule.** Even if absence did evaluate as 0, a 24-hour window with hourly evaluation would see
  "no success" in the hour before each daily run whenever the run lands a few minutes later than the day before.
  The ticket asks for 24 to 26 hours of tolerance for that reason.

**Verdict:** the metric alert is free (two time series, inside the 10 free; §3), but it cannot express 26 h, and its
behaviour on "never ran" is undocumented. Not recommended as the only signal.

## 2. Logs: which table, and is it on by default

### Which tables this environment writes

`container-env.bicep` sets `appLogsConfiguration.destination: 'log-analytics'` with the workspace's `customerId` and
shared key. That is the "Log Analytics" destination, "the default storage and viewing option"
(<https://learn.microsoft.com/en-us/azure/container-apps/log-options>). With it, system logs are queried from
**`ContainerAppSystemLogs_CL`** and console output (stdout/stderr) from **`ContainerAppConsoleLogs_CL`**, the
"Custom logs" tables (<https://learn.microsoft.com/en-us/azure/container-apps/log-monitoring>). No diagnostic setting
is needed and nothing else must be switched on.

The resource-specific tables without `_CL` (`ContainerAppSystemLogs`, `ContainerAppConsoleLogs`) belong to the
`azure-monitor` destination, which needs a diagnostic setting
(<https://learn.microsoft.com/en-us/azure/container-apps/log-options>). Their schemas have `JobName`, `Reason`, `Type`
and `Log` columns (<https://learn.microsoft.com/en-us/azure/azure-monitor/reference/tables/containerappsystemlogs>,
<https://learn.microsoft.com/en-us/azure/azure-monitor/reference/tables/containerappconsolelogs>). In the `_CL` tables
the same fields carry a type suffix. The docs show `ContainerAppName_s`, `EnvironmentName_s`, `Log_s`,
`RevisionName_s` and `ContainerName_s`. **`JobName_s` and `Reason_s` are not shown on any Microsoft docs page
(Unconfirmed)**, though they follow the same naming.

Ingestion latency: "Expect a delay of several minutes before new `ContainerAppConsoleLogs` and
`ContainerAppSystemLogs` records are queryable" (<https://learn.microsoft.com/en-us/azure/container-apps/log-options>).
That is negligible against a 26-hour window.

### Which record means "succeeded"

- **System logs:** **Unconfirmed.** No Microsoft page lists the system-log `Reason` values for a job execution
  (`ReplicaCompleted`, `ReplicaFailed`, `JobExecution...` or others). The table is documented only as covering
  "revision management, Dapr, Keda and Envoy"
  (<https://learn.microsoft.com/en-us/azure/azure-monitor/reference/tables/containerappsystemlogs>). To find out, run
  `ContainerAppSystemLogs_CL | where TimeGenerated > ago(2d) | summarize count() by Reason_s, Type_s` in the workspace
  after a known-good and a known-failed run.
- **Execution status** is documented, but only through the ARM API (`properties.status`, enum above). Log alerts
  cannot read it, and the history keeps only "the most recent 100 successful and failed job executions"; Microsoft
  says to query the logs provider "to list all executions of a job"
  (<https://learn.microsoft.com/en-us/azure/container-apps/jobs>).
- **Our own console line (recommended).** Console output reaches `ContainerAppConsoleLogs_CL` with the container name
  in `ContainerName_s` (<https://learn.microsoft.com/en-us/azure/container-apps/log-monitoring>). The Bicep names the
  containers `purge` and `research-refresh`. What the code writes today:
  - `AccountPurgeJob.RunAsync` logs `Purge finished: {Purged} purged, {Skipped} skipped, {Failed} failed.` and returns
    exit 0 only when `Failed == 0`. So "`0 failed.`" maps exactly to success.
  - `ResearchRefreshJob.RefreshAsync` logs `Research refresh finished: built ...; failed ...; skipped ....`, but its exit
    code also depends on the SEC User-Agent being real, so that line alone is **not** a success marker.
  - Console output uses Serilog's `AnsiConsoleTheme.Code` (`ServiceDefaults/LoggingExtensions.cs`), which wraps
    rendered *property values* in ANSI colour codes. Match on literal message text, not on a formatted number such as
    `0 failed`.

  **So #150 should add one literal line, logged just before each job returns `StartupExit.Success`**, for example
  `Job succeeded.` with no properties. The alert keys on the container name plus that text. `Program.cs` already calls
  `Log.CloseAndFlushAsync()`, so the async sink flushes the line before the process exits.

Microsoft's own caution applies: log alerts "work best when you're trying to detect specific data in the logs, as
opposed to when you're trying to detect a lack of data", because of ingestion latency
(<https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-types>). The 26-hour window gives each job about
two hours of slack, which covers that latency.

### KQL sketches

Rules: `ago()` accepts "timespan literals only", so write `ago(26h)`, not a `let` variable. `AggregatedValue` is
reserved, and `bag_unpack()`, `pivot()` and `narrow()` are not supported
(<https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-create-log-alert-rule>).

**One job: no success in 26 h, which also fires if the job never started.** Absence is turned into a row by
anti-joining an expected list:

```kusto
datatable(Job: string) ["purge"]
| join kind=leftanti (
    ContainerAppConsoleLogs_CL
    | where TimeGenerated > ago(26h)
    | where ContainerName_s == "purge" and Log_s has "Job succeeded."
    | distinct Job = ContainerName_s
  ) on Job
```

Zero rows means healthy. One row means no success line in 26 h, whether the run failed, timed out, crashed before
logging, or was never scheduled.

**Two jobs, one rule.** Same shape, with both container names:

```kusto
datatable(Job: string) ["purge", "research-refresh"]
| join kind=leftanti (
    ContainerAppConsoleLogs_CL
    | where TimeGenerated > ago(26h)
    | where ContainerName_s in ("purge", "research-refresh") and Log_s has "Job succeeded."
    | distinct Job = ContainerName_s
  ) on Job
```

Interim variant for the Purge only, using today's line (no code change): `Log_s has "Purge finished:"` and then
check the failed count is zero. This works only while the theme is stripped, or by matching after removing ANSI
codes: `replace_regex(Log_s, @"\x1b\[[0-9;]*m", "")`. Because that depends on how the theme renders, the dedicated
literal line is the better option.

**System-log variant (only if the Reason is confirmed).** Replace the inner query with
`ContainerAppSystemLogs_CL | where TimeGenerated > ago(26h) | where Reason_s == "<confirmed success reason>" | distinct Job = JobName_s`
and list the two job resource names in the `datatable`. This would also catch a run the app never got to log, but
it rests on two unconfirmed column/value facts.

## 3. Pricing (current)

The pricing page states the billing rules
(<https://azure.microsoft.com/en-us/pricing/details/monitor/>):

- Metric alerts: "billed per each time series monitored", with a free quota of "10 monitored metric time-series per
  month".
- Log search alerts: "billed by the interval at which the query is executed". For each frequency the rule includes its
  "First time-series", and each extra dimension (time series) costs more.
- Email notifications: a free quota of "1,000 emails per month". Notifications are "charged separately" from the
  alert rule.

The dollar amounts, from the Azure Retail Prices API
(`https://prices.azure.com/api/retail/prices?$filter=serviceName eq 'Azure Monitor' and armRegionName eq 'westus2'`,
queried 2026-10-27; these alert meters are not regional, per
<https://learn.microsoft.com/en-us/azure/azure-monitor/fundamentals/cost-meters>):

| Meter (Cost Management name) | Unit | Price (USD) |
|---|---|---|
| Alerts Metric Monitored | per time series a month | first 10 free, then **$0.10** |
| Alerts Dynamic Threshold | per time series a month | $0.10 (not needed here) |
| Alerts System Log Monitored at 15 Minute Frequency | per log alert rule a month | **$0.50** |
| Alerts System Log Monitored at 10 Minute Frequency | per rule a month | $1.00 |
| Alerts System Log Monitored at 5 Minute Frequency | per rule a month | $1.50 |
| Alerts System Log Monitored at 1 Minute Frequency | per rule a month | $3.00 (simple log search alerts are "billed the same as 1-min frequency alerts", <https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-types>) |
| Alerts Resource Monitored at 15 / 10 / 5 / 1 Minute Frequency | per **extra** time series a month | $0.05 / $0.10 / $0.15 / $0.30 |
| Emails | per email | first 1,000 free, then $0.00002 (= $2 per 100,000) |

**Log alert at 1 h, 6 h or 24 h: Unconfirmed price, probably $0.50.** The Retail Prices API and the meter list
(<https://learn.microsoft.com/en-us/azure/azure-monitor/fundamentals/cost-meters>) have log-alert meters only for
1, 5, 10 and 15 minutes. No meter exists for a slower frequency, so an hourly or daily rule is presumably billed on
the 15-minute meter at $0.50 a month. No Microsoft page says so explicitly. Read it as **at most $0.50**, and check it
in Cost Management after the first month.

Querying Analytics-plan logs is free ("There's no charge for querying this data except for Basic and Auxiliary
logs"), and so is collecting platform metrics
(<https://learn.microsoft.com/en-us/azure/azure-monitor/fundamentals/cost-usage>). The success lines add a few hundred
bytes a day to Analytics Logs ingestion. On Pay-as-you-go "The first 5 GB/month per billing account in this tier are
free" (<https://azure.microsoft.com/en-us/pricing/details/monitor/>), so that cost is nil at this volume.

## 4. Bicep: resource types, API versions and gotchas

| Resource | Newest GA version | Newest preview | Notes |
|---|---|---|---|
| `Microsoft.Insights/actionGroups` | `2023-01-01` | `2024-10-01-preview` | `location: 'global'` in the reference sample; `groupShortName` max 12 chars and required; `enabled` required; `emailReceivers[]` takes `name`, `emailAddress`, `useCommonAlertSchema` (<https://learn.microsoft.com/en-us/azure/templates/microsoft.insights/actiongroups>) |
| `Microsoft.Insights/scheduledQueryRules` | `2026-03-01` (also `2023-12-01`, `2022-06-15`, `2021-08-01`) | `2025-01-01-preview` | `kind: 'LogAlert'` (default) (<https://learn.microsoft.com/en-us/azure/templates/microsoft.insights/scheduledqueryrules>) |
| `Microsoft.Insights/metricAlerts` | `2026-01-01` (also `2018-03-01`) | `2024-03-01-preview` | `location: 'global'` in the samples (<https://learn.microsoft.com/en-us/azure/templates/microsoft.insights/metricalerts>) |

Version 2026-03-01 is new. `2023-12-01` is the conservative GA choice if the Bicep linter in CI has no types for
2026-03-01 yet. That is **Unconfirmed**; run `az bicep build` to find out.

Gotchas, each from the cited page:

1. **Frequency range.** Log alert "Frequency of evaluation … anywhere from one minute to one day (24 hours)"
   (<https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-create-log-alert-rule>).
2. **Statefulness (`autoMitigate`) caps the frequency.** "Stateful alert rules with a frequency of more than 12 hours
   are not supported." A stateful rule resolves after the condition is not met for "two frequency periods" when the
   frequency is 15 minutes to 11 hours (same page). So a `P1D` rule **must** set `autoMitigate: false`, and a
   stateless rule notifies again every time it evaluates true. `autoMitigate` defaults to `true`
   (<https://learn.microsoft.com/en-us/azure/templates/microsoft.insights/scheduledqueryrules>).
3. **Query time range.** It defaults to `windowSize × numberOfEvaluationPeriods`, and `overrideQueryTimeRange`
   changes it (template reference). "The alert time range is limited to a maximum of two days", and an `ago()` longer
   than that is clipped to two days (rule-creation page). For a 26-hour look-back with an hourly rule, set
   `overrideQueryTimeRange: 'P2D'` and let `ago(26h)` in the query narrow it. **Unconfirmed:** whether the API also
   accepts `windowSize: 'PT26H'` directly. Validate with `az deployment group what-if`.
4. **Identity.** Without a managed identity, "the alert rule will inherit the permissions of the last user or service
   principal who edited it". With a managed identity, it needs a *reader* role on every workspace the query touches
   (rule-creation page). If the rule is deployed from CI with no identity, it runs as the CI service principal, which
   is fine while that principal can read the workspace. `SystemAssigned` plus a role assignment needs the deployer to
   hold `Microsoft.Authorization/roleAssignments/write`.
5. **`ago()` literals, reserved `AggregatedValue`, 64 KB** total rule size (rule-creation page).
6. **Metric alert `autoMitigate`** defaults to `true`. A stateful metric alert "is resolved automatically when the
   alert condition isn't met for three consecutive evaluations"
   (<https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-troubleshoot-metric>).
7. **Splitting by dimension costs extra:** "each time series that results from the combination triggers its own alert
   and is charged separately" (rule-creation page). Leave the two-job rule unsplit to keep one time series (the fired
   alert's results show which job is missing), or split on `Job` for one alert per job at +$0.05 a month.
8. Metric alerts on a new resource: "It can take 10 to 15 minutes after you create the resource for a metric to be
   available", and `skipMetricValidation` exists for metrics not yet emitted
   (<https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-create-metric-alert-rule>,
   <https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-troubleshoot-metric>). This only matters if the
   metric route is chosen.

## 5. Recommendation (within $2 a month)

**Signal:** our own literal success line in `ContainerAppConsoleLogs_CL` (one line per job, logged only on exit 0),
read by **one log search alert rule** with the "expected jobs anti-join" query from §2. It meets the ticket's semantics:
"no successful execution in about 26 h", including a job that failed, timed out, crashed, or never started.

**Rule shape** (for #499 to lock):

```bicep
resource jobsMissedSuccess 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '<prefix>-jobs-missed-success'
  location: location                     // the workspace region
  kind: 'LogAlert'
  properties: {
    severity: 2
    enabled: true
    scopes: [ logAnalyticsWorkspaceId ]
    evaluationFrequency: 'PT1H'          // alert within ~1 h of the 26 h deadline
    windowSize: 'PT1H'
    overrideQueryTimeRange: 'P2D'        // query narrows itself with ago(26h)
    criteria: {
      allOf: [
        {
          query: '<two-job KQL from §2>'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: { numberOfEvaluationPeriods: 1, minFailingPeriodsToAlert: 1 }
        }
      ]
    }
    autoMitigate: true                   // stateful: one email, auto-resolves 2 periods after the next success
    actions: { actionGroups: [ actionGroupId ] }
  }
}
```

**Monthly cost:**

| Item | Cost |
|---|---|
| 1 log search alert rule, 1 h frequency, no dimension split | $0.50 (15-minute meter; billing for slower frequencies is **Unconfirmed**, see §3) |
| Action Group with one email receiver; at most ~30 emails a month (stateful: one when fired, one when resolved) | $0.00 (inside the 1,000 free) |
| Extra log ingestion (two short lines a day) | $0.00 (inside the 5 GB free) |
| **Total** | **≈ $0.50 a month** (≤ $0.55 if split per job) |

**Why not the cheaper metric alert ($0):** it cannot look back more than 24 h, the `state` values are undocumented,
and nothing documents it firing when the job never runs. It could be added later as a free second signal for *failed*
executions (`Executions`, `state = Failed`, Total > 0) once the dimension values are confirmed in the portal.

**Why hourly, not daily:** both bill at about the same price. A `P1D` rule cannot auto-resolve (gotcha 2) and could
detect a miss up to 24 h late, while `PT1H` alerts within an hour and clears itself.

## Unconfirmed facts (summary)

1. The values of the `Executions` metric's `state` dimension (presumably the `JobExecutionRunningState` strings).
2. Whether a metric alert evaluates an absent `state = Succeeded` series as 0. It probably does not, given the
   "sparse metric" guidance.
3. The `_CL` column names `JobName_s` and `Reason_s` in `ContainerAppSystemLogs_CL`, and the `Reason` value of a
   successful (or failed) job execution. No Microsoft page lists them.
4. The price of a log search alert at 1 h, 6 h or 24 h frequency. No meter exists beyond 15 minutes, so it is
   presumably the $0.50 15-minute rate.
5. Whether `windowSize: 'PT26H'` is accepted, as opposed to `overrideQueryTimeRange: 'P2D'` plus `ago(26h)`.
6. Whether the CI Bicep tooling already knows `scheduledQueryRules@2026-03-01`. `@2023-12-01` is the safe GA pick.
7. The largest `evaluationFrequency` a metric alert accepts.
