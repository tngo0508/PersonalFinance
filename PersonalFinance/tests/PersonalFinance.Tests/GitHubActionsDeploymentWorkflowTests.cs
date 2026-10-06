using System.Text.RegularExpressions;
using Xunit;

namespace PersonalFinance.Tests;

public class GitHubActionsDeploymentWorkflowTests
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
    public void DeploymentWorkflow_ExistsAndIsNonEmpty()
    {
        var root = FindProjectRoot();
        var workflowPath = Path.Combine(root, ".github", "workflows", "deploy-azure.yml");

        Assert.True(File.Exists(workflowPath), $"Expected workflow file '{workflowPath}' does not exist.");
        var content = File.ReadAllText(workflowPath);
        Assert.False(string.IsNullOrWhiteSpace(content), "Workflow file is unexpectedly empty.");
    }

    [Fact]
    public void DeploymentWorkflow_ConfiguresRequiredTriggers()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // Push to main
        Assert.Contains("push:", workflow);
        Assert.Contains("branches:", workflow);
        Assert.Contains("- main", workflow);

        // Pull request validation
        Assert.Contains("pull_request:", workflow);

        // Manual workflow dispatch with parameter inputs
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("environment:", workflow);
        Assert.Contains("deploy_sql:", workflow);
        Assert.Contains("dry_run:", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_ConfiguresZeroCostRegistry_GHCR()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // GHCR configuration
        Assert.Contains("REGISTRY: ghcr.io", workflow);
        Assert.Contains("registry: ${{ env.REGISTRY }}", workflow);
        Assert.Contains("password: ${{ secrets.GITHUB_TOKEN }}", workflow);

        // Multi-project container targets
        Assert.Contains("PersonalFinance/src/PersonalFinance.ApiService/Dockerfile", workflow);
        Assert.Contains("PersonalFinance/src/PersonalFinance.Web/Dockerfile", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_ConfiguresPasswordlessAzureOidcAuthentication()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // OIDC permission token
        Assert.Contains("id-token: write", workflow);
        Assert.Contains("packages: write", workflow);
        Assert.Contains("contents: read", workflow);

        // Azure Login with OIDC secrets
        Assert.Contains("azure/login@v2", workflow);
        Assert.Contains("client-id: ${{ secrets.AZURE_CLIENT_ID }}", workflow);
        Assert.Contains("tenant-id: ${{ secrets.AZURE_TENANT_ID }}", workflow);
        Assert.Contains("subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}", workflow);
        Assert.Contains("uses: azure/cli@v2", workflow);
        Assert.Contains("az group create", workflow);
        Assert.Contains("Wait for stale Container Apps environment deletion", workflow);
        Assert.Contains("ScheduledForDelete", workflow);
        Assert.Contains("Show Azure deployment operations on failure", workflow);
        Assert.Contains("az deployment operation group list", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_BuildAndTestStage_CoversDotNet10AndBicepValidation()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Assert.Contains("build-and-test:", workflow);
        Assert.Contains("actions/setup-dotnet@v4", workflow);
        Assert.Contains("dotnet restore PersonalFinance.sln", workflow);
        Assert.Contains("dotnet build PersonalFinance.sln", workflow);
        Assert.Contains("dotnet test PersonalFinance.sln", workflow);
        Assert.Contains("az bicep build --file infra/main.bicep", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_DeployStage_MapsAllExpectedBicepParameters()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Assert.Contains("deploy-azure:", workflow);
        Assert.Contains("template: infra/main.bicep", workflow);
        Assert.Contains("environmentName=${{ inputs.environment || env.ENVIRONMENT_NAME }}", workflow);
        Assert.Contains("location=${{ env.AZURE_LOCATION }}", workflow);
        Assert.Contains("sqlLocation=${{ env.AZURE_SQL_LOCATION }}", workflow);
        Assert.Contains("apiImage=${{ needs.build-and-push-containers.outputs.api_image_tag }}", workflow);
        Assert.Contains("webImage=${{ needs.build-and-push-containers.outputs.web_image_tag }}", workflow);
        Assert.Contains("googleClientId=${{ secrets.GOOGLE_CLIENT_ID }}", workflow);
        Assert.Contains("googleClientSecret=${{ secrets.GOOGLE_CLIENT_SECRET }}", workflow);
        Assert.Contains("brevoApiKey=${{ secrets.BREVO_API_KEY }}", workflow);
        Assert.Contains("googleDriveApiKey=${{ secrets.GOOGLE_DRIVE_API_KEY }}", workflow);
        Assert.Contains("deploySqlDatabase=${{ github.event_name != 'workflow_dispatch' || inputs.deploy_sql == true }}", workflow);
        Assert.Contains("customConnectionString=${{ secrets.CUSTOM_CONNECTION_STRING }}", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_AllJobAndOutputReferences_AreValidAndDeclared()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // 1. Discover all defined job IDs and declared outputs using structural line analysis
        var lines = workflow.Split('\n');
        string? currentJob = null;
        bool inOutputs = false;
        var declaredOutputs = new Dictionary<string, HashSet<string>>();
        var definedJobs = new HashSet<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            // Job definition at 2-space indentation (e.g. "  build-and-push-containers:")
            if (Regex.IsMatch(line, @"^  [a-zA-Z0-9_-]+:"))
            {
                currentJob = line.Trim().TrimEnd(':');
                definedJobs.Add(currentJob);
                declaredOutputs[currentJob] = new HashSet<string>();
                inOutputs = false;
            }
            else if (currentJob != null && Regex.IsMatch(line, @"^    outputs:"))
            {
                inOutputs = true;
            }
            else if (inOutputs)
            {
                var outputMatch = Regex.Match(line, @"^      ([a-zA-Z0-9_-]+):");
                if (outputMatch.Success)
                {
                    declaredOutputs[currentJob!].Add(outputMatch.Groups[1].Value);
                }
                else if (Regex.IsMatch(line, @"^    [a-zA-Z0-9_-]+:"))
                {
                    inOutputs = false;
                }
            }
        }

        Assert.Contains("build-and-test", definedJobs);
        Assert.Contains("build-and-push-containers", definedJobs);
        Assert.Contains("deploy-azure", definedJobs);
        Assert.Contains("smoke-test", definedJobs);

        Assert.Contains("api_image_tag", declaredOutputs["build-and-push-containers"]);
        Assert.Contains("web_image_tag", declaredOutputs["build-and-push-containers"]);

        // 2. Find all expressions referencing needs.<job_id>.outputs.<output_name>
        var needsOutputReferences = Regex.Matches(workflow, @"needs\.([a-zA-Z0-9_-]+)\.outputs\.([a-zA-Z0-9_-]+)");
        Assert.NotEmpty(needsOutputReferences);

        foreach (Match refMatch in needsOutputReferences)
        {
            var targetJobId = refMatch.Groups[1].Value;
            var outputName = refMatch.Groups[2].Value;

            Assert.True(definedJobs.Contains(targetJobId),
                $"Workflow expression references job '{targetJobId}' which is NOT defined under jobs in the workflow.");

            Assert.True(declaredOutputs.ContainsKey(targetJobId) && declaredOutputs[targetJobId].Contains(outputName),
                $"Workflow expression references output '{outputName}' from job '{targetJobId}', but it is not declared under outputs for that job.");
        }
    }

    [Fact]
    public void DeploymentWorkflow_SmokeTestBashScript_HasNoPowerShellBacktickEscapes()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        var smokeTestJobMatch = Regex.Match(workflow, @"smoke-test:\s*.*", RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");

        var smokeTestContent = smokeTestJobMatch.Value;

        // Ensure there are no PowerShell-style backtick escapes (e.g. `n, `r, `t) in bash script blocks
        Assert.DoesNotContain("`n", smokeTestContent);
        Assert.DoesNotContain("`r", smokeTestContent);
        Assert.DoesNotContain("`t", smokeTestContent);
    }

    [Fact]
    public void DeploymentWorkflow_SmokeTestStage_VerifiesCoreEndpoints()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Assert.Contains("smoke-test:", workflow);
        Assert.Contains("/health", workflow);
        Assert.Contains("/alive", workflow);
        Assert.Contains("${WEB_URL}/", workflow);
        Assert.Contains("/Identity/Account/Login", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_AllJobs_DefineExplicitTimeouts()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        var timeoutMatches = Regex.Matches(workflow, @"timeout-minutes:\s*(\d+)");
        Assert.Equal(4, timeoutMatches.Count);

        // Verify smoke-test job specifically limits execution duration
        var smokeTestJobMatch = Regex.Match(workflow, @"\n  smoke-test:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)", RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");
        Assert.Contains("timeout-minutes:", smokeTestJobMatch.Value);
    }

    [Fact]
    public void DeploymentWorkflow_SmokeTestJob_ConfiguresCurlTimeoutsAndFallback()
    {
        var root = FindProjectRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        var smokeTestJobMatch = Regex.Match(workflow, @"\n  smoke-test:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)", RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");
        var content = smokeTestJobMatch.Value;

        // Verify connection timeout and max time are enforced on curl calls to prevent hanging
        Assert.Contains("--connect-timeout", content);
        Assert.Contains("--max-time", content);
        Assert.Contains("echo \"000\"", content);
    }

    [Fact]
    public void SmokeTestScript_ExistsAndDefinesExpectedEndpointsAndOptions()
    {
        var root = FindProjectRoot();
        var scriptPath = Path.Combine(root, "infra", "smoke-test.ps1");

        Assert.True(File.Exists(scriptPath), $"Expected smoke test script '{scriptPath}' does not exist.");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("[string]$WebUrl", script);
        Assert.Contains("[int]$MaxRetries", script);
        Assert.Contains("/health", script);
        Assert.Contains("/alive", script);
        Assert.Contains("/Identity/Account/Login", script);
        Assert.Contains("Invoke-WebRequest", script);
    }

    [Fact]
    public void Readme_ContainsDeploymentStatusBadgeIndicator()
    {
        var root = FindProjectRoot();
        var readmePath = Path.Combine(root, "README.md");

        Assert.True(File.Exists(readmePath), "README.md must exist in the repository root.");
        var readme = File.ReadAllText(readmePath);

        Assert.Contains("https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml/badge.svg", readme);
        Assert.Contains("https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml", readme);
    }
}
