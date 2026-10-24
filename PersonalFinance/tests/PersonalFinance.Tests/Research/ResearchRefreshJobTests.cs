using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The <c>--refresh-research</c> mode the daily Container Apps Job runs: it rebuilds the Research datasets and exits,
/// failing the run when a dataset could not be rebuilt or SEC's required User-Agent contact is not configured.
/// </summary>
public class ResearchRefreshJobTests
{
    private sealed class FakeResearchService(params string[] failed) : IResearchService
    {
        public int Refreshes { get; private set; }

        public Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<SectorsSnapshotDto>(null));

        public Task<ResearchRefreshSummary> RefreshAsync(CancellationToken cancellationToken = default)
        {
            Refreshes++;
            var summary = new ResearchRefreshSummary();
            summary.Built.Add("sectors");
            summary.Failed.AddRange(failed);
            if (failed.Length > 0)
            {
                summary.Built.Clear();
            }

            return Task.FromResult(summary);
        }
    }

    private static ServiceProvider Services(FakeResearchService service, string userAgent) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton(new SecEdgarSettings { UserAgent = userAgent })
            .AddSingleton<IResearchService>(service)
            .BuildServiceProvider();

    [Theory]
    [InlineData(new[] { "--refresh-research" }, true)]
    [InlineData(new[] { "--purge-only" }, false)]
    [InlineData(new string[0], false)]
    public void RefreshResearch_IsRequestedByTheArgument(string[] args, bool expected)
    {
        Assert.Equal(expected, ResearchRefreshJob.IsRequested(args));
    }

    [Fact]
    public async Task RefreshAsync_WhenEveryDatasetBuilds_Succeeds()
    {
        var service = new FakeResearchService();
        await using ServiceProvider services = Services(service, "PersonalFinance ops@example.org");

        int exitCode = await ResearchRefreshJob.RefreshAsync(services);

        Assert.Equal(StartupExit.Success, exitCode);
        Assert.Equal(1, service.Refreshes);
    }

    [Fact]
    public async Task RefreshAsync_WhenADatasetFails_FailsTheRun()
    {
        var service = new FakeResearchService("sectors");
        await using ServiceProvider services = Services(service, "PersonalFinance ops@example.org");

        Assert.Equal(StartupExit.Failure, await ResearchRefreshJob.RefreshAsync(services));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PersonalFinance research contact@example.com")] // the committed placeholder
    public async Task RefreshAsync_WithoutAConfiguredUserAgent_FailsWithoutCallingSec(string userAgent)
    {
        var service = new FakeResearchService();
        await using ServiceProvider services = Services(service, userAgent);

        Assert.Equal(StartupExit.Failure, await ResearchRefreshJob.RefreshAsync(services));
        Assert.Equal(0, service.Refreshes);
    }
}
