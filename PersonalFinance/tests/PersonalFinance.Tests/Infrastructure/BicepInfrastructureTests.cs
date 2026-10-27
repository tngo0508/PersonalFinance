using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PersonalFinance.ApiService.Features.GoogleDrive.Adapters;

namespace PersonalFinance.Tests.Infrastructure;

public class BicepInfrastructureTests
{
    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PersonalFinance.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root directory.");
    }

    [Fact]
    public void MainBicep_DeclaresExpectedModulesAndZeroCostDefaults()
    {
        string root = FindProjectRoot();
        string mainBicep = File.ReadAllText(Path.Combine(root, "infra", "main.bicep"));

        Assert.Contains("targetScope = 'resourceGroup'", mainBicep);
        Assert.Contains("module logAnalytics 'modules/log-analytics.bicep'", mainBicep);
        Assert.Contains("module containerEnv 'modules/container-env.bicep'", mainBicep);
        Assert.Contains("module sqlDatabase 'modules/sql-database.bicep'", mainBicep);
        Assert.Contains("module apiService 'modules/api-service.bicep'", mainBicep);
        Assert.Contains("module webService 'modules/web-service.bicep'", mainBicep);
        Assert.Contains("module purgeJob 'modules/purge-job.bicep'", mainBicep);
        Assert.Contains("module researchRefreshJob 'modules/research-refresh-job.bicep'", mainBicep);

        Assert.Contains("output webUrl string", mainBicep);
        Assert.Contains("output webFqdn string", mainBicep);
        Assert.Contains("output apiFqdn string", mainBicep);
        Assert.Contains("output containerAppEnvironmentName string", mainBicep);
        Assert.Contains("param logRetentionInDays int = 30", mainBicep);
        Assert.Contains("retentionInDays: logRetentionInDays", mainBicep);
        Assert.Contains("param sqlLocation string", mainBicep);
        Assert.Contains("location: sqlLocation", mainBicep);
        Assert.Contains("uniqueString(resourceGroup().id, location)", mainBicep);
    }

    [Fact]
    public void PurgeJobBicep_RunsTheApiImageDaily_WithPurgeOnly_AndTheConnectionStringAsASecret()
    {
        string root = FindProjectRoot();
        string jobBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "purge-job.bicep"));
        string mainBicep = File.ReadAllText(Path.Combine(root, "infra", "main.bicep"));

        // A scheduled job, because the API scales to zero and cannot be relied on to run every day (ADR 0005)
        Assert.Contains("resource purgeJob 'Microsoft.App/jobs@", jobBicep);
        Assert.Contains("triggerType: 'Schedule'", jobBicep);
        Assert.Contains("cronExpression: cronExpression", jobBicep);
        Assert.Contains("parallelism: 1", jobBicep);
        Assert.Contains("workloadProfileName: 'Consumption'", jobBicep);

        // Same image as the API, in purge mode
        Assert.Contains("'--purge-only'", jobBicep);
        Assert.Contains("containerImage: apiImage", mainBicep);
        Assert.Contains("param purgeJobCron string = '0 15 * * *'", mainBicep);

        // The connection string only ever travels as a secret
        Assert.Matches(@"@secure\(\)\s+param dbConnectionString string", jobBicep);
        Assert.Contains("secretRef: 'db-connection-string'", jobBicep);
        Assert.Contains("name: 'ConnectionStrings__DefaultConnection'", jobBicep);
    }

    [Fact]
    public void ResearchRefreshJobBicep_RunsTheApiImageDaily_WithRefreshResearch_TheSecContact_AndTheConnectionStringAsASecret()
    {
        string root = FindProjectRoot();
        string jobBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "research-refresh-job.bicep"));
        string mainBicep = File.ReadAllText(Path.Combine(root, "infra", "main.bicep"));

        // A scheduled job, because the API scales to zero and cannot be relied on to refresh every day
        Assert.Contains("resource researchRefreshJob 'Microsoft.App/jobs@", jobBicep);
        Assert.Contains("triggerType: 'Schedule'", jobBicep);
        Assert.Contains("cronExpression: cronExpression", jobBicep);
        Assert.Contains("parallelism: 1", jobBicep);
        Assert.Contains("workloadProfileName: 'Consumption'", jobBicep);

        // Same image as the API, in refresh mode, off-peak
        Assert.Contains("'--refresh-research'", jobBicep);
        Assert.Contains("param researchRefreshJobCron string = '0 10 * * *'", mainBicep);
        Assert.Contains("cronExpression: researchRefreshJobCron", mainBicep);

        // SEC's required contact comes from deployment configuration, never from code
        Assert.Contains("name: 'Research__Sec__UserAgent'", jobBicep);
        Assert.Contains("secUserAgent: secUserAgent", mainBicep);

        // The connection string only ever travels as a secret
        Assert.Matches(@"@secure\(\)\s+param dbConnectionString string", jobBicep);
        Assert.Contains("secretRef: 'db-connection-string'", jobBicep);
        Assert.Contains("name: 'ConnectionStrings__DefaultConnection'", jobBicep);
    }

    [Fact]
    public void ApiServiceBicep_ConfiguresInternalIngress_ScaleToZero_AndHealthProbes()
    {
        string root = FindProjectRoot();
        string apiBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "api-service.bicep"));

        // Scale-to-zero for zero cost
        Assert.Contains("param minReplicas int = 0", apiBicep);
        Assert.Contains("minReplicas: minReplicas", apiBicep);

        // Internal ingress on port 8080 by default; public exposure is opt-in
        Assert.Contains("param exposePublicly bool = false", apiBicep);
        Assert.Contains("external: exposePublicly", apiBicep);
        Assert.Contains("name: 'ApiDocs__Enabled'", apiBicep);
        Assert.Contains("targetPort: 8080", apiBicep);

        // Probes
        Assert.Contains("path: '/alive'", apiBicep);
        Assert.Contains("path: '/health'", apiBicep);
        Assert.Contains("type: 'Liveness'", apiBicep);
        Assert.Contains("type: 'Readiness'", apiBicep);
        Assert.Contains("type: 'Startup'", apiBicep);

        // Secrets & Env
        Assert.Contains("google-drive-api-key", apiBicep);
        Assert.Contains("db-connection-string", apiBicep);
        Assert.Contains("GoogleDrive__ApiKey", apiBicep);
    }

    [Fact]
    public void WebServiceBicep_ConfiguresExternalIngress_ScaleToZero_ForwardedHeaders_AndSecrets()
    {
        string root = FindProjectRoot();
        string webBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "web-service.bicep"));

        // Scale-to-zero for zero cost
        Assert.Contains("param minReplicas int = 0", webBicep);
        Assert.Contains("minReplicas: minReplicas", webBicep);

        // External ingress on port 8080
        Assert.Contains("external: true", webBicep);
        Assert.Contains("targetPort: 8080", webBicep);

        // Probes
        Assert.Contains("path: '/alive'", webBicep);
        Assert.Contains("path: '/health'", webBicep);

        // Forwarded headers for SSL termination & OAuth
        Assert.Contains("ASPNETCORE_FORWARDEDHEADERS_ENABLED", webBicep);

        // Secrets
        Assert.Contains("google-client-id", webBicep);
        Assert.Contains("google-client-secret", webBicep);
        Assert.Contains("brevo-api-key", webBicep);
        Assert.Contains("db-connection-string", webBicep);

        // Env vars
        Assert.Contains("Authentication__Google__ClientId", webBicep);
        Assert.Contains("Authentication__Google__ClientSecret", webBicep);
        Assert.Contains("Brevo__ApiKey", webBicep);
        Assert.Contains("services__apiservice__http__0", webBicep);
        // Ensure misleading http url with https key is not present
        Assert.DoesNotContain("services__apiservice__https__0", webBicep);
    }

    [Fact]
    public void LogAnalyticsBicep_ConfiguresPerGB2018Sku_AndRetention()
    {
        string root = FindProjectRoot();
        string laBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "log-analytics.bicep"));

        Assert.Contains("Microsoft.OperationalInsights/workspaces", laBicep);
        Assert.Contains("name: 'PerGB2018'", laBicep);
        Assert.Contains("retentionInDays", laBicep);
        Assert.Contains("param retentionInDays int = 30", laBicep);
        Assert.Contains("param dailyQuotaGb int = -1", laBicep);
    }

    [Fact]
    public void ContainerEnvBicep_ConfiguresConsumptionWorkloadProfile()
    {
        string root = FindProjectRoot();
        string caeBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "container-env.bicep"));

        Assert.Contains("Microsoft.App/managedEnvironments", caeBicep);
        Assert.Contains("workloadProfileType: 'Consumption'", caeBicep);
        Assert.Contains("destination: 'log-analytics'", caeBicep);
    }

    [Fact]
    public void SqlDatabaseBicep_ConfiguresBasicTier()
    {
        string root = FindProjectRoot();
        string sqlBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "sql-database.bicep"));

        Assert.Contains("Microsoft.Sql/servers", sqlBicep);
        Assert.Contains("Microsoft.Sql/servers/databases", sqlBicep);
        Assert.Contains("name: 'Basic'", sqlBicep);
        Assert.Contains("tier: 'Basic'", sqlBicep);
        Assert.Contains("AllowAllWindowsAzureIps", sqlBicep);
    }

    [Fact]
    public void MainParametersJson_IsValidJson_AndContainsCoreKeys()
    {
        string root = FindProjectRoot();
        string jsonContent = File.ReadAllText(Path.Combine(root, "infra", "main.parameters.json"));

        using var doc = JsonDocument.Parse(jsonContent);
        JsonElement rootElem = doc.RootElement;
        JsonElement parameters = rootElem.GetProperty("parameters");

        Assert.True(parameters.TryGetProperty("environmentName", out _));
        Assert.True(parameters.TryGetProperty("location", out _));
        Assert.True(parameters.TryGetProperty("apiImage", out _));
        Assert.True(parameters.TryGetProperty("webImage", out _));
        Assert.True(parameters.TryGetProperty("deploySqlDatabase", out _));
        Assert.True(parameters.TryGetProperty("logRetentionInDays", out _));
    }

    [Fact]
    public void DeployScript_ContainsCoreAzureCliCommands_AndSwitches()
    {
        string root = FindProjectRoot();
        string scriptContent = File.ReadAllText(Path.Combine(root, "infra", "deploy.ps1"));

        Assert.Contains("[switch]$ValidateOnly", scriptContent);
        Assert.Contains("[switch]$WhatIf", scriptContent);
        Assert.Contains("[switch]$DeploySqlDatabase", scriptContent);
        Assert.Contains("az deployment group validate", scriptContent);
        Assert.Contains("az deployment group what-if", scriptContent);
        Assert.Contains("az deployment group create", scriptContent);
        Assert.DoesNotContain("$dynamicParams -join", scriptContent);
    }

    [Fact]
    public void ProgramCs_Web_ConfiguresForwardedHeaders_ForReverseProxy()
    {
        string root = FindProjectRoot();
        string programCs =
            File.ReadAllText(Path.Combine(root, "PersonalFinance", "src", "PersonalFinance.Web", "Program.cs"));

        Assert.Contains("UseForwardedHeaders", programCs);
        Assert.Contains("ForwardedHeadersOptions", programCs);
        Assert.Contains("XForwardedProto", programCs);
    }
}
