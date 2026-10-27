using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

public class GitHubActionsDeploymentWorkflowTests
{
    // The deploy steps and the smoke-test script are composite actions shared by deploy-azure.yml and rollback.yml
    private static string ReadRepoFile(params string[] relativePath) =>
        File.ReadAllText(Path.Combine([RepositoryRoot.Find(), .. relativePath]));

    private static string DeployAction() => ReadRepoFile(".github", "actions", "deploy-azure", "action.yml");

    private static string SmokeTestAction() => ReadRepoFile(".github", "actions", "smoke-test", "action.yml");

    [Fact]
    public void DeploymentWorkflow_ConfiguresRequiredTriggers()
    {
        string root = RepositoryRoot.Find();
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
        string root = RepositoryRoot.Find();
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
        string root = RepositoryRoot.Find();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // OIDC permission token
        Assert.Contains("id-token: write", workflow);
        Assert.Contains("packages: write", workflow);
        Assert.Contains("contents: read", workflow);

        // The workflow hands the OIDC secrets to the shared deploy action
        Assert.Contains("uses: ./.github/actions/deploy-azure", workflow);
        Assert.Contains("azure_client_id: ${{ secrets.AZURE_CLIENT_ID }}", workflow);
        Assert.Contains("azure_tenant_id: ${{ secrets.AZURE_TENANT_ID }}", workflow);
        Assert.Contains("azure_subscription_id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}", workflow);

        // Azure Login with OIDC, inside the shared deploy action
        string action = DeployAction();
        Assert.Contains("azure/login@v2", action);
        Assert.Contains("client-id: ${{ inputs.azure_client_id }}", action);
        Assert.Contains("tenant-id: ${{ inputs.azure_tenant_id }}", action);
        Assert.Contains("subscription-id: ${{ inputs.azure_subscription_id }}", action);
        Assert.Contains("uses: azure/cli@v2", action);
        Assert.Contains("az group exists", action);
        Assert.Contains("az group create", action);
        Assert.Contains("Wait for stale Container Apps environment deletion", action);
        Assert.Contains("ScheduledForDelete", action);
        Assert.Contains("Show Azure deployment operations on failure", action);
        Assert.Contains("az deployment operation group list", action);
    }

    [Fact]
    public void DeploymentWorkflow_BuildAndTestStage_CoversDotNet10AndBicepValidation()
    {
        string root = RepositoryRoot.Find();
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
        string root = RepositoryRoot.Find();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // The repo-root global.json pins the SDK, so CI and local builds run the same analyzers (#125)
        Assert.True(File.Exists(Path.Combine(root, "global.json")), "global.json must exist at the repository root.");

        MatchCollection setupSteps =
            Regex.Matches(workflow, @"uses: actions/setup-dotnet@v4\s+with:\s+(?<input>[^\r\n]+)");
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
        string root = RepositoryRoot.Find();
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
        string root = RepositoryRoot.Find();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // The workflow maps its values onto the shared deploy action's inputs...
        Assert.Contains("deploy-azure:", workflow);
        Assert.Contains("environment: ${{ inputs.environment || env.ENVIRONMENT_NAME }}", workflow);
        Assert.Contains("location: ${{ env.AZURE_LOCATION }}", workflow);
        Assert.Contains("sql_location: ${{ env.AZURE_SQL_LOCATION }}", workflow);
        Assert.Contains("api_image: ${{ needs.build-and-push-containers.outputs.api_image_tag }}", workflow);
        Assert.Contains("web_image: ${{ needs.build-and-push-containers.outputs.web_image_tag }}", workflow);
        Assert.Contains("google_client_id: ${{ secrets.GOOGLE_CLIENT_ID }}", workflow);
        Assert.Contains("google_client_secret: ${{ secrets.GOOGLE_CLIENT_SECRET }}", workflow);
        Assert.Contains("brevo_api_key: ${{ secrets.BREVO_API_KEY }}", workflow);
        Assert.Contains("google_drive_api_key: ${{ secrets.GOOGLE_DRIVE_API_KEY }}", workflow);
        Assert.Contains("deploy_sql: ${{ github.event_name != 'workflow_dispatch' || inputs.deploy_sql == true }}",
            workflow);
        Assert.Contains("custom_connection_string: ${{ secrets.CUSTOM_CONNECTION_STRING }}", workflow);

        // ...and the action maps those inputs onto the Bicep parameters
        string action = DeployAction();
        Assert.Contains("template: infra/main.bicep", action);
        Assert.Contains("environmentName=${{ inputs.environment }}", action);
        Assert.Contains("location=${{ inputs.location }}", action);
        Assert.Contains("exposeApiPublicly=", action);
        Assert.Contains("sqlLocation=${{ inputs.sql_location }}", action);
        Assert.Contains("apiImage=${{ inputs.api_image }}", action);
        Assert.Contains("webImage=${{ inputs.web_image }}", action);
        Assert.Contains("googleClientId=${{ inputs.google_client_id }}", action);
        Assert.Contains("googleClientSecret=${{ inputs.google_client_secret }}", action);
        Assert.Contains("brevoApiKey=${{ inputs.brevo_api_key }}", action);
        Assert.Contains("googleDriveApiKey=${{ inputs.google_drive_api_key }}", action);
        Assert.Contains("deploySqlDatabase=${{ inputs.deploy_sql }}", action);
        Assert.Contains("customConnectionString=${{ inputs.custom_connection_string }}", action);
    }

    [Fact]
    public void DeploymentWorkflow_AllJobAndOutputReferences_AreValidAndDeclared()
    {
        string root = RepositoryRoot.Find();
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
        MatchCollection needsOutputReferences =
            Regex.Matches(workflow, @"needs\.([a-zA-Z0-9_-]+)\.outputs\.([a-zA-Z0-9_-]+)");
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
    public void WorkflowsAndActions_HaveNoPowerShellBacktickEscapes()
    {
        // Every run: step is bash, where a PowerShell escape such as `n is a literal backtick. The smoke test's script
        // moved into .github/actions/smoke-test, so every workflow and action is scanned, not one job of one file.
        string root = RepositoryRoot.Find();
        string[] files = Directory.GetFiles(Path.Combine(root, ".github"), "*.yml", SearchOption.AllDirectories);

        List<string> offenders = files
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, index, line)))
            .Where(entry => Regex.IsMatch(entry.line, "`[nrt]"))
            .Select(entry => $"{Path.GetRelativePath(root, entry.file)}:{entry.index + 1}")
            .ToList();

        Assert.Contains(files, file => file.EndsWith(Path.Combine("smoke-test", "action.yml"), StringComparison.Ordinal));
        Assert.True(offenders.Count == 0, $"PowerShell backtick escapes in bash: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void DeploymentWorkflow_SmokeTestStage_VerifiesCoreEndpoints()
    {
        string root = RepositoryRoot.Find();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Assert.Contains("smoke-test:", workflow);
        Assert.Contains("uses: ./.github/actions/smoke-test", workflow);
        Assert.Contains("web_url: ${{ needs.deploy-azure.outputs.web_url }}", workflow);

        string action = SmokeTestAction();
        Assert.Contains("/health", action);
        Assert.Contains("/alive", action);
        Assert.Contains("${WEB_URL}/", action);
        Assert.Contains("/Identity/Account/Login", action);
    }

    [Fact]
    public void DeploymentWorkflow_AllJobs_DefineExplicitTimeouts()
    {
        string root = RepositoryRoot.Find();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        // Every job (2-space indented key under `jobs:`) must declare its own timeout
        string jobsSection = workflow[workflow.IndexOf("\njobs:", StringComparison.Ordinal)..];
        MatchCollection jobBlocks =
            Regex.Matches(jobsSection, @"\n  ([a-zA-Z0-9_-]+):[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)");
        Assert.NotEmpty(jobBlocks);
        foreach (Match job in jobBlocks)
        {
            Assert.True(Regex.IsMatch(job.Value, @"\n    timeout-minutes:\s*\d+"),
                $"Job '{job.Groups[1].Value}' must define timeout-minutes.");
        }

        // Verify smoke-test job specifically limits execution duration
        Match smokeTestJobMatch = Regex.Match(workflow, @"\n  smoke-test:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)",
            RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");
        Assert.Contains("timeout-minutes:", smokeTestJobMatch.Value);
    }

    [Fact]
    public void DeploymentWorkflow_PullRequests_AreOptimizedForSpeed()
    {
        string root = RepositoryRoot.Find();
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
        Assert.Contains("needs: [build-and-test, sql-server-migrations, validate-infra, build-and-push-containers]",
            workflow);

        // `latest` only moves after a successful deploy + smoke test
        Assert.Contains("promote-latest:", workflow);
        Assert.Contains("needs: [build-and-push-containers, smoke-test]", workflow);
        Assert.DoesNotContain("type=raw,value=latest", workflow);
    }

    [Fact]
    public void DeploymentWorkflow_SqlServerMigrationsJob_AppliesMigrationsTwiceWithTheDeveloperScripts()
    {
        string root = RepositoryRoot.Find();
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
        string root = RepositoryRoot.Find();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy-azure.yml"));

        Match smokeTestJobMatch = Regex.Match(workflow, @"\n  smoke-test:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)",
            RegexOptions.Singleline);
        Assert.True(smokeTestJobMatch.Success, "Could not find smoke-test job in workflow.");
        Assert.Contains("uses: ./.github/actions/smoke-test", smokeTestJobMatch.Value);

        // Verify connection timeout and max time are enforced on curl calls to prevent hanging
        string content = SmokeTestAction();
        Assert.Contains("--connect-timeout", content);
        Assert.Contains("--max-time", content);
        Assert.Contains("echo \"000\"", content);

        // No PowerShell-style backtick escapes in the bash script
        Assert.DoesNotContain("`n", content);
        Assert.DoesNotContain("`r", content);
        Assert.DoesNotContain("`t", content);
    }

    [Fact]
    public void ContainerImages_PublishWithTheAppVersion()
    {
        string workflow = ReadRepoFile(".github", "workflows", "deploy-azure.yml");

        // The version comes from AppVersion.cs, the same source create-release tags from (ADR 0006)
        Assert.Contains("app_version=$(bash .github/scripts/release-version.sh current)", workflow);
        Assert.Contains("APP_VERSION=${{ steps.tags.outputs.app_version }}", workflow);

        foreach (string project in new[] { "PersonalFinance.ApiService", "PersonalFinance.Web" })
        {
            string dockerfile = ReadRepoFile("PersonalFinance", "src", project, "Dockerfile");
            string publishStage = dockerfile[dockerfile.IndexOf("FROM build AS publish", StringComparison.Ordinal)..];
            Assert.Contains("ARG APP_VERSION=", publishStage);
            Assert.Contains("/p:Version=$APP_VERSION", publishStage);
        }
    }

    [Fact]
    public void RollbackWorkflow_RedeploysATagWithTheSharedStepsWithoutOverlappingADeploy()
    {
        string rollback = ReadRepoFile(".github", "workflows", "rollback.yml");
        string deploy = ReadRepoFile(".github", "workflows", "deploy-azure.yml");

        Assert.Contains("workflow_dispatch:", rollback);
        Assert.Contains("tag:", rollback);
        Assert.Contains("allow_schema_drift:", rollback);

        // The same deploy and smoke-test steps; the Bicep parameters live only in the shared action
        Assert.Contains("uses: ./.github/actions/deploy-azure", rollback);
        Assert.Contains("uses: ./.github/actions/smoke-test", rollback);
        Assert.DoesNotContain("infra/main.bicep", rollback);
        Assert.DoesNotContain("template: infra/main.bicep", deploy);

        // The literal group a deploy-azure.yml run on main evaluates to, never cancelled
        Assert.Contains("group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}", deploy);
        string deployWorkflowName = Regex.Match(deploy, @"^name: (?<name>[^\r\n]+)").Groups["name"].Value;
        Assert.Contains($"group: {deployWorkflowName}-refs/heads/main", rollback);
        Assert.Contains("cancel-in-progress: false", rollback);

        // Safety checks before anything deploys
        Assert.Contains("github.ref != 'refs/heads/main'", rollback);
        Assert.Contains("^{commit}", rollback);
        Assert.Contains("docker manifest inspect", rollback);
        Assert.Contains("Migrations/AzureSql", rollback);

        // Tags and Releases are never touched: without contents: write the token cannot create either
        Assert.DoesNotContain("contents: write", rollback);
        Assert.DoesNotContain("gh release", rollback);
        Assert.DoesNotContain("git push", rollback);

        // The free-text tag reaches scripts only through env, never interpolated into a run block
        foreach (Match run in Regex.Matches(rollback, @"run: \|\r?\n(?<body>(?:[ ]{10,}[^\r\n]*\r?\n|\r?\n)+)"))
        {
            Assert.DoesNotContain("${{ inputs.", run.Groups["body"].Value);
        }

        // Every job declares its own timeout
        string jobsSection = rollback[rollback.IndexOf("\njobs:", StringComparison.Ordinal)..];
        MatchCollection jobBlocks =
            Regex.Matches(jobsSection, @"\n  ([a-zA-Z0-9_-]+):[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)");
        Assert.Equal(4, jobBlocks.Count);
        foreach (Match job in jobBlocks)
        {
            Assert.True(Regex.IsMatch(job.Value, @"\n    timeout-minutes:\s*\d+"),
                $"Rollback job '{job.Groups[1].Value}' must define timeout-minutes.");
        }
    }

    [Fact]
    public void SmokeTestScript_ExistsAndDefinesExpectedEndpointsAndOptions()
    {
        string root = RepositoryRoot.Find();
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
        string root = RepositoryRoot.Find();
        string readmePath = Path.Combine(root, "README.md");

        Assert.True(File.Exists(readmePath), "README.md must exist in the repository root.");
        string readme = File.ReadAllText(readmePath);

        Assert.Contains("https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml/badge.svg",
            readme);
        Assert.Contains("https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml", readme);
    }
}
