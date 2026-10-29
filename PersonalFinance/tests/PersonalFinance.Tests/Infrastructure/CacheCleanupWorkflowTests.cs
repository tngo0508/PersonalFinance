using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The Cache Cleanup workflow keeps the repository under the free 10 GB Actions cache limit. Every deploy leaves
/// about 250 MB of image layers that no later build reads, and a csproj change leaves a superseded 1 GB NuGet
/// cache behind: without a daily prune GitHub's own least-recently-used eviction removes the NuGet and Playwright
/// caches too. These tests pin the schedule, the idle window and the dry-run guard around every delete.
/// </summary>
public class CacheCleanupWorkflowTests
{
    // The working copy may be CRLF; the line-anchored assertions below are written for LF.
    private static string Workflow() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Find(), ".github", "workflows", "cache-cleanup.yml"))
            .Replace("\r\n", "\n");

    private static string PruneJob()
    {
        Match job = Regex.Match(Workflow(), @"\n  prune-stale-caches:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)");
        Assert.True(job.Success, "Could not find the prune-stale-caches job in cache-cleanup.yml.");
        return job.Value;
    }

    [Fact]
    public void Workflow_RunsDaily_OnClosedPullRequests_AndByHandAsADryRun()
    {
        string workflow = Workflow();

        // Deploys add 2 to 3 GB of dead image layers a day, so a weekly prune cannot stay under 10 GB.
        Assert.Contains("    - cron: '0 6 * * *'", workflow);
        Assert.Contains("  pull_request:\n    types: [closed]", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("      dry_run:\n", workflow);
        Assert.Contains("        default: true", workflow);
        Assert.Contains("permissions:\n  actions: write", workflow);
    }

    [Fact]
    public void PruneJob_DeletesDockerBlobsIdleForADay_MeasuredFromTheNewestRead()
    {
        string job = PruneJob();

        // The reusable blobs (base image and restore layers) are read on every build; a deploy's own publish
        // layers are never read again, so one day of idleness separates the two. Measuring from the newest
        // read, not from now, keeps a quiet week from emptying the cache.
        Assert.Contains("if: github.event_name != 'pull_request'", job);
        Assert.Contains(@"startswith(""buildkit-"") or startswith(""index-"")", job);
        Assert.Contains("map(.lastAccessedAt | ts) | max", job);
        Assert.Contains("$newest - 24 * 3600", job);
        Assert.DoesNotContain("7 * 86400", job);
    }

    [Fact]
    public void PruneJob_KeepsOnlyTheNewestNuGetPlaywrightAndNpmCachePerRef()
    {
        string job = PruneJob();

        // Each key ends in a hash of its inputs: a csproj change creates a new entry and strands the old one.
        Assert.Contains(@"test(""^(nuget|playwright|node-cache)-"")", job);
        Assert.Contains("group_by([.ref,", job);
        Assert.Contains("sort_by(.createdAt) | .[:-1][]", job);
    }

    [Fact]
    public void PruneJob_GuardsEveryDeleteWithTheDryRunFlag_AndReportsTheUsageAfterwards()
    {
        string job = PruneJob();

        int deletes = Regex.Count(job, @"gh cache delete");
        int guards = Regex.Count(job, @"if \[ ""\$DRY_RUN"" != ""true"" \]");
        Assert.True(deletes >= 2, "Expected the Docker prune and the superseded-cache prune each to delete.");
        Assert.Equal(deletes, guards);
        Assert.Contains("actions/cache/usage", job);
    }

    [Fact]
    public void ClosedPullRequest_DeletesOnlyThatPullRequestsCaches()
    {
        Match job = Regex.Match(Workflow(), @"\n  delete-pr-caches:[\s\S]*?(?=\n  [a-zA-Z0-9_-]+:|$)");
        Assert.True(job.Success, "Could not find the delete-pr-caches job in cache-cleanup.yml.");

        Assert.Contains("if: github.event_name == 'pull_request'", job.Value);
        Assert.Contains("PR_REF: refs/pull/${{ github.event.pull_request.number }}/merge", job.Value);
        Assert.Contains("gh cache list --ref \"$PR_REF\"", job.Value);
    }
}
