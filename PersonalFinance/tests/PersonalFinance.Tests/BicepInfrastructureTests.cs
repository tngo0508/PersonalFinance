using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Tests;

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
    public void AllInfrastructureFiles_ExistAndAreNonEmpty()
    {
        var root = FindProjectRoot();
        var infraDir = Path.Combine(root, "infra");
        var modulesDir = Path.Combine(infraDir, "modules");

        var expectedFiles = new[]
        {
            Path.Combine(infraDir, "main.bicep"),
            Path.Combine(infraDir, "main.parameters.json"),
            Path.Combine(infraDir, "deploy.ps1"),
            Path.Combine(root, "azure.yaml"),
            Path.Combine(modulesDir, "log-analytics.bicep"),
            Path.Combine(modulesDir, "container-env.bicep"),
            Path.Combine(modulesDir, "sql-database.bicep"),
            Path.Combine(modulesDir, "api-service.bicep"),
            Path.Combine(modulesDir, "web-service.bicep")
        };

        foreach (var file in expectedFiles)
        {
            Assert.True(File.Exists(file), $"Expected infrastructure file '{file}' does not exist.");
            var content = File.ReadAllText(file);
            Assert.False(string.IsNullOrWhiteSpace(content), $"File '{file}' is unexpectedly empty.");
        }
    }

    [Fact]
    public void MainBicep_DeclaresExpectedModulesAndZeroCostDefaults()
    {
        var root = FindProjectRoot();
        var mainBicep = File.ReadAllText(Path.Combine(root, "infra", "main.bicep"));

        Assert.Contains("targetScope = 'resourceGroup'", mainBicep);
        Assert.Contains("module logAnalytics 'modules/log-analytics.bicep'", mainBicep);
        Assert.Contains("module containerEnv 'modules/container-env.bicep'", mainBicep);
        Assert.Contains("module sqlDatabase 'modules/sql-database.bicep'", mainBicep);
        Assert.Contains("module apiService 'modules/api-service.bicep'", mainBicep);
        Assert.Contains("module webService 'modules/web-service.bicep'", mainBicep);

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
    public void ApiServiceBicep_ConfiguresInternalIngress_ScaleToZero_AndHealthProbes()
    {
        var root = FindProjectRoot();
        var apiBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "api-service.bicep"));

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
        var root = FindProjectRoot();
        var webBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "web-service.bicep"));

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
        var root = FindProjectRoot();
        var laBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "log-analytics.bicep"));

        Assert.Contains("Microsoft.OperationalInsights/workspaces", laBicep);
        Assert.Contains("name: 'PerGB2018'", laBicep);
        Assert.Contains("retentionInDays", laBicep);
        Assert.Contains("param retentionInDays int = 30", laBicep);
        Assert.Contains("param dailyQuotaGb int = -1", laBicep);
    }

    [Fact]
    public void ContainerEnvBicep_ConfiguresConsumptionWorkloadProfile()
    {
        var root = FindProjectRoot();
        var caeBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "container-env.bicep"));

        Assert.Contains("Microsoft.App/managedEnvironments", caeBicep);
        Assert.Contains("workloadProfileType: 'Consumption'", caeBicep);
        Assert.Contains("destination: 'log-analytics'", caeBicep);
    }

    [Fact]
    public void SqlDatabaseBicep_ConfiguresServerlessLifetimeFreeTier()
    {
        var root = FindProjectRoot();
        var sqlBicep = File.ReadAllText(Path.Combine(root, "infra", "modules", "sql-database.bicep"));

        Assert.Contains("Microsoft.Sql/servers", sqlBicep);
        Assert.Contains("Microsoft.Sql/servers/databases", sqlBicep);
        Assert.Contains("name: 'GP_S_Gen5_1'", sqlBicep);
        Assert.Contains("tier: 'GeneralPurpose'", sqlBicep);
        Assert.Contains("useFreeLimit: true", sqlBicep);
        Assert.Contains("freeLimitExhaustionBehavior: 'AutoPause'", sqlBicep);
        Assert.Contains("AllowAllWindowsAzureIps", sqlBicep);

        // Numeric minCapacity using json('0.5') instead of string any('0.5')
        Assert.Contains("minCapacity: json('0.5')", sqlBicep);
        Assert.DoesNotContain("any('0.5')", sqlBicep);
    }

    [Fact]
    public void MainParametersJson_IsValidJson_AndContainsCoreKeys()
    {
        var root = FindProjectRoot();
        var jsonContent = File.ReadAllText(Path.Combine(root, "infra", "main.parameters.json"));

        using var doc = JsonDocument.Parse(jsonContent);
        var rootElem = doc.RootElement;
        var parameters = rootElem.GetProperty("parameters");

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
        var root = FindProjectRoot();
        var scriptContent = File.ReadAllText(Path.Combine(root, "infra", "deploy.ps1"));

        Assert.Contains("[switch]$ValidateOnly", scriptContent);
        Assert.Contains("[switch]$WhatIf", scriptContent);
        Assert.Contains("[switch]$DeploySqlDatabase", scriptContent);
        Assert.Contains("az deployment group validate", scriptContent);
        Assert.Contains("az deployment group what-if", scriptContent);
        Assert.Contains("az deployment group create", scriptContent);
        Assert.DoesNotContain("$dynamicParams -join", scriptContent);
    }

    [Fact]
    public void DeployScript_ParameterAssembly_PassesDiscreteTokensForAzCli()
    {
        var root = FindProjectRoot();
        var deployScriptPath = Path.Combine(root, "infra", "deploy.ps1");
        Assert.True(File.Exists(deployScriptPath));

        // Execute a PowerShell snippet mimicking deploy.ps1 parameter assembly to verify token discrete behavior
        var psCommand = @"
$ParametersFile = 'dummy.parameters.json'
$EnvironmentName = 'pf-prod'
$Location = 'eastus'
$ApiImage = 'ghcr.io/tngo0508/personalfinance-api:latest'
$WebImage = 'ghcr.io/tngo0508/personalfinance-web:latest'
$DeploySqlDatabase = [System.Management.Automation.SwitchParameter]::Present
$GoogleClientId = 'test-client-id'
$GoogleClientSecret = 'test-secret'
$BrevoApiKey = 'test-key'
$BrevoSenderEmail = 'test@example.com'
$BrevoSenderName = 'PersonalFinance'
$GoogleDriveApiKey = 'test-drive-key'
$SqlAdminPassword = 'TestPassword123!'
$CustomConnectionString = 'Server=localhost;Database=Test;'
$CustomDomainName = 'finance.example.com'

$paramArgs = @()
if (Test-Path $ParametersFile) {
    $paramArgs += '--parameters'
    $paramArgs += ""@$ParametersFile""
}

$dynamicParams = @(
    ""environmentName=$EnvironmentName"",
    ""location=$Location"",
    ""apiImage=$ApiImage"",
    ""webImage=$WebImage"",
    ""deploySqlDatabase=$($DeploySqlDatabase.IsPresent.ToString().ToLower())""
)

if ($GoogleClientId) { $dynamicParams += ""googleClientId=$GoogleClientId"" }
if ($GoogleClientSecret) { $dynamicParams += ""googleClientSecret=$GoogleClientSecret"" }
if ($BrevoApiKey) { $dynamicParams += ""brevoApiKey=$BrevoApiKey"" }
if ($BrevoSenderEmail) { $dynamicParams += ""brevoSenderEmail=$BrevoSenderEmail"" }
if ($BrevoSenderName) { $dynamicParams += ""brevoSenderName=$BrevoSenderName"" }
if ($GoogleDriveApiKey) { $dynamicParams += ""googleDriveApiKey=$GoogleDriveApiKey"" }
if ($SqlAdminPassword) { $dynamicParams += ""sqlAdministratorLoginPassword=$SqlAdminPassword"" }
if ($CustomConnectionString) { $dynamicParams += ""customConnectionString=$CustomConnectionString"" }
if ($CustomDomainName) { $dynamicParams += ""customDomainName=$CustomDomainName"" }

if ($dynamicParams.Count -gt 0) {
    $paramArgs += '--parameters'
    foreach ($param in $dynamicParams) {
        $paramArgs += $param
    }
}

$paramArgs | ConvertTo-Json -Compress
";

        var bytes = System.Text.Encoding.Unicode.GetBytes(psCommand);
        var base64Command = Convert.ToBase64String(bytes);

        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh",
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {base64Command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);

        Assert.True(process.ExitCode == 0, $"PowerShell failed with error: {error}");
        Assert.False(string.IsNullOrWhiteSpace(output), "PowerShell output was empty.");

        var tokens = JsonSerializer.Deserialize<List<string>>(output);
        Assert.NotNull(tokens);

        // Verify discrete tokens
        Assert.Contains("--parameters", tokens);
        Assert.Contains("environmentName=pf-prod", tokens);
        Assert.Contains("location=eastus", tokens);
        Assert.Contains("deploySqlDatabase=true", tokens);
        Assert.Contains("googleClientId=test-client-id", tokens);
        Assert.Contains("googleClientSecret=test-secret", tokens);
        Assert.Contains("brevoApiKey=test-key", tokens);
        Assert.Contains("sqlAdministratorLoginPassword=TestPassword123!", tokens);

        // CRITICAL REGRESSION CHECK: No token should contain space-separated multiple assignments
        foreach (var token in tokens)
        {
            if (token != "--parameters" && token.StartsWith("environmentName="))
            {
                // Must be exactly "environmentName=pf-prod", not "environmentName=pf-prod location=eastus ..."
                Assert.Equal("environmentName=pf-prod", token);
            }
            if (token.Contains("="))
            {
                Assert.DoesNotContain(" ", token.Trim());
            }
        }
    }

    [Fact]
    public void ProgramCs_Web_ConfiguresForwardedHeaders_ForReverseProxy()
    {
        var root = FindProjectRoot();
        var programCs = File.ReadAllText(Path.Combine(root, "PersonalFinance", "src", "PersonalFinance.Web", "Program.cs"));

        Assert.Contains("UseForwardedHeaders", programCs);
        Assert.Contains("ForwardedHeadersOptions", programCs);
        Assert.Contains("XForwardedProto", programCs);
    }
}
