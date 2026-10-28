using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalFinance.ApiService.Features.AccountLifecycle;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.AccountLifecycle;

/// <summary>
/// The <c>--purge-only</c> mode the daily Container Apps Job runs: it fails the run when any Account could not be
/// purged, and logs the job-succeeded line the job alert looks for only when the run succeeds.
/// </summary>
public class AccountPurgeJobTests
{
    private sealed class FakePurgeService(Func<AccountPurgeSummary> purge) : IAccountPurgeService
    {
        public Task<AccountPurgeSummary> PurgeDueAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(purge());
    }

    private static ServiceProvider Services(Func<AccountPurgeSummary> purge, RecordingLoggerProvider logs) =>
        new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(logs))
            .AddSingleton<IAccountPurgeService>(new FakePurgeService(purge))
            .BuildServiceProvider();

    [Fact]
    public async Task PurgeAsync_WhenEveryAccountIsPurged_SucceedsAndLogsTheJobSucceededLine()
    {
        var logs = new RecordingLoggerProvider();
        await using ServiceProvider services = Services(() => new AccountPurgeSummary(2, 1, 0), logs);

        Assert.Equal(StartupExit.Success, await AccountPurgeJob.PurgeAsync(services));
        Assert.Single(logs.Messages, m => m == StartupExit.JobSucceededMessage);
    }

    [Fact]
    public async Task PurgeAsync_WhenAnAccountFails_FailsWithoutTheJobSucceededLine()
    {
        var logs = new RecordingLoggerProvider();
        await using ServiceProvider services = Services(() => new AccountPurgeSummary(2, 0, 1), logs);

        Assert.Equal(StartupExit.Failure, await AccountPurgeJob.PurgeAsync(services));
        Assert.DoesNotContain(StartupExit.JobSucceededMessage, logs.Messages);
    }

    [Fact]
    public async Task PurgeAsync_WhenStoppedBeforeTheEnd_FailsWithoutTheJobSucceededLine()
    {
        var logs = new RecordingLoggerProvider();
        await using ServiceProvider services = Services(() => throw new OperationCanceledException(), logs);

        Assert.Equal(StartupExit.Failure, await AccountPurgeJob.PurgeAsync(services));
        Assert.DoesNotContain(StartupExit.JobSucceededMessage, logs.Messages);
    }
}
