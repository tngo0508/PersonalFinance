using System.Text.RegularExpressions;
using PersonalFinance.Shared.Constants;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// One GitHub Release per merged PR, tagged from AppVersion with notes from its CHANGELOG section (ADR 0006).
/// </summary>
public class ReleaseAutomationTests
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

    private static string ReadRepoFile(params string[] path) =>
        File.ReadAllText(Path.Combine([FindProjectRoot(), .. path]));

    private static string JobBlock(string workflow, string jobId)
    {
        var match = Regex.Match(workflow, $@"\n  {Regex.Escape(jobId)}:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)");
        Assert.True(match.Success, $"Could not find job '{jobId}' in the workflow.");
        return match.Value;
    }

    [Fact]
    public void AppVersion_Current_MatchesItsComponents()
    {
        // The release tag is v{Current}; a stale Current would tag the wrong version
        Assert.Equal(AppVersion.FullVersion, AppVersion.Current);
    }

    [Fact]
    public void Changelog_HasADatedSectionForTheCurrentVersion_AndNoUnreleasedSection()
    {
        var changelog = ReadRepoFile("CHANGELOG.md");

        Assert.Matches(
            new Regex($@"^## \[{Regex.Escape(AppVersion.Current)}\] - \d{{4}}-\d{{2}}-\d{{2}}\r?$", RegexOptions.Multiline),
            changelog);
        Assert.DoesNotMatch(new Regex(@"^## \[Unreleased\]", RegexOptions.Multiline), changelog);
    }

    [Fact]
    public void DeploymentWorkflow_CreateReleaseJob_ReleasesOnlyAfterSmokeTestsOnMain()
    {
        var workflow = ReadRepoFile(".github", "workflows", "deploy-azure.yml");
        var job = JobBlock(workflow, "create-release");

        Assert.Contains("needs: smoke-test", job);
        Assert.Contains("if: ${{ github.ref == format('refs/heads/{0}', github.event.repository.default_branch) }}", job);
        Assert.Contains("contents: write", job);
        Assert.Contains("bash .github/scripts/release-version.sh current", job);
        Assert.Contains("bash .github/scripts/release-version.sh notes", job);

        // An existing tag skips the release instead of failing the deployment
        Assert.Contains("git ls-remote --exit-code --tags origin \"refs/tags/$TAG\"", job);
        Assert.Contains("exit 0", job);

        Assert.Contains("--target \"$GITHUB_SHA\"", job);
        Assert.Contains("--notes-file", job);
        Assert.Contains("--generate-notes", job);
        Assert.Contains("--prerelease --latest=false", job);
    }

    [Fact]
    public void ReleaseCheckWorkflow_RunsOnEveryPullRequest_AndAllowsDocsOnlyAndNoReleasePullRequests()
    {
        var workflow = ReadRepoFile(".github", "workflows", "release-check.yml");

        // A required status check must always report, so the workflow is never path-filtered
        Assert.DoesNotMatch(new Regex(@"^\s*(paths|paths-ignore):", RegexOptions.Multiline), workflow);
        Assert.Contains("types: [opened, synchronize, reopened, labeled, unlabeled]", workflow);
        Assert.Contains("timeout-minutes:", workflow);

        Assert.Contains("contains(github.event.pull_request.labels.*.name, 'no-release')", workflow);
        Assert.Contains("grep -q -v -E '\\.md$'", workflow);

        Assert.Contains("release-version.sh current \"$RUNNER_TEMP/BaseAppVersion.cs\"", workflow);
        Assert.Contains("release-version.sh is-newer \"$VERSION\" \"$BASE_VERSION\"", workflow);
        Assert.Contains("release-version.sh notes \"$VERSION\"", workflow);
    }

    [Fact]
    public void ReleaseVersionScript_DefinesTheCommandsTheWorkflowsUse()
    {
        var script = ReadRepoFile(".github", "scripts", "release-version.sh");

        Assert.StartsWith("#!/usr/bin/env bash", script);
        Assert.Contains("set -euo pipefail", script);
        Assert.Contains("PersonalFinance/src/PersonalFinance.Shared/Constants/AppVersion.cs", script);
        Assert.Contains("current) shift; cmd_current", script);
        Assert.Contains("is-newer) shift;", script);
        Assert.Contains("notes) shift;", script);
    }
}
