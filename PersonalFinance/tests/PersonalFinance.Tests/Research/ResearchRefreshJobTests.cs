using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The <c>--refresh-research</c> mode the daily Container Apps Job runs: it rebuilds the Research datasets and exits,
/// failing the run when a dataset could not be rebuilt or SEC's required User-Agent contact is not configured (the
/// World Bank datasets are still rebuilt then).
/// </summary>
public class ResearchRefreshJobTests
{
    private sealed class FakeResearchService(params string[] failed) : IResearchService
    {
        public int Refreshes { get; private set; }

        public ResearchSources? Sources { get; private set; }

        public Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<SectorsSnapshotDto>(null));

        public Task<ResearchSnapshotResult<FundsSnapshotDto>> GetFundsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundsSnapshotDto>(null));

        public Task<ResearchSnapshotResult<FundOverlapSnapshotDto>> GetFundOverlapAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundOverlapSnapshotDto>(null));

        public Task<ResearchSnapshotResult<FundReturnsSnapshotDto>> GetFundReturnsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundReturnsSnapshotDto>(null));

        public Task<ResearchSnapshotResult<MacroSnapshotDto>> GetMacroAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<MacroSnapshotDto>(null));

        public Task<ResearchSnapshotResult<FundFactsSnapshotDto>> GetFundFactsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundFactsSnapshotDto>(null));

        public Task<ResearchSnapshotResult<FundExposureSnapshotDto>> GetFundExposureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundExposureSnapshotDto>(null));

        public Task<ResearchSnapshotResult<FundIndustriesSnapshotDto>> GetFundIndustriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundIndustriesSnapshotDto>(null));

        public Task<ResearchSnapshotResult<WorldSnapshotDto>> GetWorldAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<WorldSnapshotDto>(null));

        public Task<ResearchSearchIndexDto> GetSearchIndexAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSearchIndexDto());

        public Task<ResearchStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchStatusDto());

        public Task<ResearchRefreshSummary> RefreshAsync(ResearchSources sources = ResearchSources.All,
            CancellationToken cancellationToken = default)
        {
            Refreshes++;
            Sources = sources;
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

    private static ServiceProvider Services(FakeResearchService service, string userAgent,
        RecordingLoggerProvider? logs = null) =>
        new ServiceCollection()
            .AddLogging(builder =>
            {
                if (logs is not null)
                {
                    builder.AddProvider(logs);
                }
            })
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
        Assert.Equal(ResearchSources.All, service.Sources);
    }

    [Fact]
    public async Task RefreshAsync_WhenADatasetFails_FailsTheRun()
    {
        var service = new FakeResearchService("sectors");
        await using ServiceProvider services = Services(service, "PersonalFinance ops@example.org");

        Assert.Equal(StartupExit.Failure, await ResearchRefreshJob.RefreshAsync(services));
    }

    [Fact]
    public async Task RefreshAsync_WhenItSucceeds_LogsTheJobSucceededLine()
    {
        var logs = new RecordingLoggerProvider();
        await using ServiceProvider services = Services(new FakeResearchService(), "PersonalFinance ops@example.org", logs);

        Assert.Equal(StartupExit.Success, await ResearchRefreshJob.RefreshAsync(services));

        // The job alert looks for this exact line; it stays free of properties (Serilog colours them on the console).
        Assert.Single(logs.Messages, m => m == StartupExit.JobSucceededMessage);
    }

    [Theory]
    [InlineData(new[] { "sectors" }, "PersonalFinance ops@example.org")]
    [InlineData(new string[0], "PersonalFinance research contact@example.com")] // SEC skipped
    public async Task RefreshAsync_WhenItFails_DoesNotLogTheJobSucceededLine(string[] failed, string userAgent)
    {
        var logs = new RecordingLoggerProvider();
        await using ServiceProvider services = Services(new FakeResearchService(failed), userAgent, logs);

        Assert.Equal(StartupExit.Failure, await ResearchRefreshJob.RefreshAsync(services));
        Assert.DoesNotContain(StartupExit.JobSucceededMessage, logs.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PersonalFinance research contact@example.com")] // the committed placeholder
    public async Task RefreshAsync_WithoutAConfiguredUserAgent_StillRefreshesTheWorldBankDatasetsAndTheRates_AndFails(
        string userAgent)
    {
        var service = new FakeResearchService();
        await using ServiceProvider services = Services(service, userAgent);

        // SEC's datasets are skipped (SEC's fair-access rules need a real contact), World and the exchange rates (which
        // need no contact) are still refreshed, and the run fails so the missing setting shows in the job's history.
        Assert.Equal(StartupExit.Failure, await ResearchRefreshJob.RefreshAsync(services));
        Assert.Equal(1, service.Refreshes);
        Assert.Equal(ResearchSources.WorldBank | ResearchSources.CurrencyRates, service.Sources);
    }
}
