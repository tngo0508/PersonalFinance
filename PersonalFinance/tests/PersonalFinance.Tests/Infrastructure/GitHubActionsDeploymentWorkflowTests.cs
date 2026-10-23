using System.Text.RegularExpressions;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

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
        string root = FindProjectRoot();
        string workflowPath = Path.Combine(root, ".github", "workflows", "deploy-azure.yml");

        Assert.True(File.Exists(workflowPath), $"Expected workflow file '{workflowPath}' does not exist.");
        string content = File.ReadAllText(workflowPath);
        Assert.False(string.IsNullOrWhiteSpace(content), "Workflow file is unexpectedly empty.");
    }

    [Fact]
    public void DeploymentWorkflow_ConfiguresRequiredTriggers()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

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
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

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
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

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
        Assert.Contains("az group exists", workflow);
        Assert.Contains("az group create", workflow);
        Assert.Contains("Wait for stale Container Apps environment deletion", workflow);
        Assert.Contains("ScheduledForDelete", workflow);
        Assert.Contains("Show Azure deployment operations on failure", workflow);
        Assert.Contains("az deployment operation group list", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_BuildAndTestStage_CoversDotNet10AndBicepValidation()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Assert.Contains("build-and-test:", workflow);
        Assert.Contains("actions/setup-dotnet@v4", workflow);
        Assert.Contains("dotnet restore PersonalFinance.sln", workflow);
        Assert.Contains("dotnet build PersonalFinance.sln", workflow);
        Assert.Contains("dotnet test PersonalFinance.sln", workflow);
        Assert.Contains("az bicep build --file infra/main.bicep", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_SetupDotNetSteps_UseTheSdkPinnedInGlobalJson()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // The repo-root global.json pins the SDK, so CI and local builds run the same analyzers (#125)
        Assert.True(File.Exists(Path.Combine(root, "global.json")), "global.json must exist at the repository root.");

        MatchCollection setupSteps = Regex.Matches(workflow, @"uses: actions/setup-dotnet@v4\s+with:\s+(?<input>[^\r\n]+)");
        Assert.NotEmpty(setupSteps);
        foreach (Match step in setupSteps)
        {
            Assert.Equal("global-json-file: global.json", step.Groups["input"].Value.Trim());
        }

        Assert.DoesNotContain("dotnet-version:", workflow);
        Assert.DoesNotContain("DOTNET_VERSION", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_FullSolutionBuilds_FailOnAnyWarning()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // Every full-solution build turns on the warnings-as-errors switch in Directory.Build.props (#132)
        MatchCollection solutionBuilds = Regex.Matches(workflow, @"dotnet build PersonalFinance\.sln[^\r\n]*");
        Assert.Equal(2, solutionBuilds.Count);
        foreach (Match build in solutionBuilds)
        {
            Assert.Contains("-p:EnforceWarningsAsErrors=true", build.Value);
        }
    }

    [Fact]
    public void DeploymentWorkflow_DeployStage_MapsAllExpectedBicepParameters()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Assert.Contains("deploy-azure:", workflow);
        Assert.Contains("template: infra/main.bicep", workflow);
        Assert.Contains("environmentName=${{ inputs.environment || env.ENVIRONMENT_NAME }}", workflow);
        Assert.Contains("location=${{ env.AZURE_LOCATION }}", workflow);
        Assert.Contains("exposeApiPublicly=", workflow);
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
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // 1. Discover all defined job IDs and declared outputs using structural line analysis
        string[] lines = workflow.Split('\n');
        string? currentJob = null;
        bool inOutputs = false;
        var declaredOutputs = new Dictionary<string, HashSet<string>>();
        var definedJobs = new HashSet<string>();

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
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
                Match outputMatch = Regex.Match(line, @"^      ([a-zA-Z0-9_-]+):");
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
        MatchCollection needsOutputReferences = Regex.Matches(workflow, @"needs\.([a-zA-Z0-9_-]+)\.outputs\.([a-zA-Z0-9_-]+)");
        Assert.NotEmpty(needsOutputReferences);

        foreach (Match refMatch in needsOutputReferences)
        {
            string targetJobId = refMatch.Groups[1].Value;
            string outputName = refMatch.Groups[2].Value;

            Assert.True(definedJobs.Contains(targetJobId),
                $"Workflow expression references job '{targetJobId}' which is NOT defined under jobs in the workflow.");

            Assert.True(declaredOutputs.ContainsKey(targetJobId) && declaredOutputs[targetJobId].Contains(outputName),
                $"Workflow expression references output '{outputName}' from job '{targetJobId}', but it is not declared under outputs for that job.");
        }
    }

    [Fact]
    public void DeploymentWorkflow_SmokeTestBashScript_HasNoPowerShellBacktickEscapes()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Match smokeTestJobMatch = Regex.Match(workflow, @"smoke-test:\s*.*", RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");

        string smokeTestContent = smokeTestJobMatch.Value;

        // Ensure there are no PowerShell-style backtick escapes (e.g. `n, `r, `t) in bash script blocks
        Assert.DoesNotContain("`n", smokeTestContent);
        Assert.DoesNotContain("`r", smokeTestContent);
        Assert.DoesNotContain("`t", smokeTestContent);
    }

    [Fact]
    public void DeploymentWorkflow_SmokeTestStage_VerifiesCoreEndpoints()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Assert.Contains("smoke-test:", workflow);
        Assert.Contains("/health", workflow);
        Assert.Contains("/alive", workflow);
        Assert.Contains("${WEB_URL}/", workflow);
        Assert.Contains("/Identity/Account/Login", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_AllJobs_DefineExplicitTimeouts()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // Every job (2-space indented key under `jobs:`) must declare its own timeout
        string jobsSection = workflow[workflow.IndexOf("\njobs:", StringComparison.Ordinal)..];
        MatchCollection jobBlocks = Regex.Matches(jobsSection, @"\n  ([a-zA-Z0-9_-]+):[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)");
        Assert.NotEmpty(jobBlocks);
        foreach (Match job in jobBlocks)
        {
            Assert.True(Regex.IsMatch(job.Value, @"\n    timeout-minutes:\s*\d+"),
                $"Job '{job.Groups[1].Value}' must define timeout-minutes.");
        }

        // Verify smoke-test job specifically limits execution duration
        Match smokeTestJobMatch = Regex.Match(workflow, @"\n  smoke-test:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)", RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");
        Assert.Contains("timeout-minutes:", smokeTestJobMatch.Value);
    }

    [Fact]
    public void DeploymentWorkflow_PullRequests_AreOptimizedForSpeed()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // Superseded PR runs are cancelled, but main deployments are never interrupted
        Assert.Contains("concurrency:", workflow);
        Assert.Contains("cancel-in-progress: ${{ github.event_name == 'pull_request' }}", workflow);

        // Docs-only changes skip the pipeline
        Assert.Contains("paths-ignore:", workflow);
        Assert.Contains("'**/*.md'", workflow);

        // Images build as parallel matrix legs with per-image cache scopes; PRs don't export cache
        Assert.Contains("matrix:", workflow);
        Assert.Contains("cache-from: type=gha,scope=${{ matrix.service }}", workflow);
        Assert.Contains("cache-to: ${{ env.PUSH_IMAGE == 'true' &&", workflow);

        // Deployment still waits for tests, migrations on SQL Server, infra validation and both images
        Assert.Contains("needs: [build-and-test, sql-server-migrations, validate-infra, build-and-push-containers]", workflow);

        // `latest` only moves after a successful deploy + smoke test
        Assert.Contains("promote-latest:", workflow);
        Assert.Contains("needs: [build-and-push-containers, smoke-test]", workflow);
        Assert.DoesNotContain("type=raw,value=latest", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_SqlServerMigrationsJob_AppliesMigrationsTwiceWithTheDeveloperScripts()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Match jobMatch = Regex.Match(workflow, @"\n  sql-server-migrations:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)");
        Assert.True(jobMatch.Success, "Could not find sql-server-migrations job in workflow.");
        string job = jobMatch.Value;

        Assert.Contains("runs-on: ubuntu-latest", job);

        // Same Compose file and npm scripts that developers use, with a password generated in the run
        Assert.Contains("npm run db:up", job);
        Assert.Contains("> .env", job);
        Assert.Contains("openssl rand", job);
        Assert.DoesNotContain("secrets.", job);

        // The production --migrate-only path, run twice; the second run must apply nothing
        Assert.Contains("--migrate-only", job);
        Assert.Contains("migrate 1", job);
        Assert.Contains("migrate 2", job);

        // Success is the exit code, and "applied nothing" is the migration history row count, not log wording (#108)
        Assert.Contains("exited with code ${status}", job);
        Assert.Contains("__EFMigrationsHistory", job);
        Assert.DoesNotContain("grep -q", job);

        // Diagnostics on failure, and the container is always stopped
        Assert.Matches(@"if: failure\(\)\s+run: \|[\s\S]*docker compose logs", job);
        Assert.Matches(@"if: always\(\)\s+run: npm run db:down", job);
    }

    [Fact]
    public void DeploymentWorkflow_SmokeTestJob_ConfiguresCurlTimeoutsAndFallback()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Match smokeTestJobMatch = Regex.Match(workflow, @"\n  smoke-test:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)", RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");
        string content = smokeTestJobMatch.Value;

        // Verify connection timeout and max time are enforced on curl calls to prevent hanging
        Assert.Contains("--connect-timeout", content);
        Assert.Contains("--max-time", content);
        Assert.Contains("echo \"000\"", content);
    }

    [Fact]
    public void SmokeTestScript_ExistsAndDefinesExpectedEndpointsAndOptions()
    {
        string root = FindProjectRoot();
        string scriptPath = Path.Combine(root, "infra", "smoke-test.ps1");

        Assert.True(File.Exists(scriptPath), $"Expected smoke test script '{scriptPath}' does not exist.");
        string script = File.ReadAllText(scriptPath);

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
        string root = FindProjectRoot();
        string readmePath = Path.Combine(root, "README.md");

        Assert.True(File.Exists(readmePath), "README.md must exist in the repository root.");
        string readme = File.ReadAllText(readmePath);

        Assert.Contains("https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml/badge.svg", readme);
        Assert.Contains("https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml", readme);
    }
}
