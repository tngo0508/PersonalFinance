using Microsoft.Extensions.Hosting;
using PersonalFinance.Tests.TestSupport;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

public class StartupExitTests
{
    [Fact]
    public void IsShutdown_HostAbortedException_IsShutdown()
    {
        // EF design-time tooling (dotnet ef) aborts the host after Build
        Assert.True(StartupExit.IsShutdown(new HostAbortedException(), CancellationToken.None));
    }

    [Fact]
    public void IsShutdown_OperationCanceledAfterStopping_IsShutdown()
    {
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();

        Assert.True(StartupExit.IsShutdown(new OperationCanceledException(), stopping.Token));
        Assert.True(StartupExit.IsShutdown(new TaskCanceledException(), stopping.Token));
    }

    [Fact]
    public void IsShutdown_OperationCanceledWhileNotStopping_IsFailure()
    {
        // e.g. a startup timeout: not a deliberate stop
        Assert.False(StartupExit.IsShutdown(new OperationCanceledException(), CancellationToken.None));
        Assert.False(StartupExit.IsShutdown(new TaskCanceledException(), CancellationToken.None));
    }

    [Fact]
    public void IsShutdown_OtherException_IsFailure()
    {
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();

        Assert.False(StartupExit.IsShutdown(new InvalidOperationException(), CancellationToken.None));
        Assert.False(StartupExit.IsShutdown(new InvalidOperationException(), stopping.Token));
    }

    [Fact]
    public void ExitCodes_FailureIsNonZero()
    {
        Assert.Equal(0, StartupExit.Success);
        Assert.NotEqual(0, StartupExit.Failure);
    }

    [Fact]
    public void JobSucceededMessage_IsWhatTheJobAlertLooksFor()
    {
        // The job alert (infra/modules/job-alerts.bicep) fires when a job logs no line containing this text for 26 h.
        // Changing it here without the query would page every day.
        Assert.Equal("Scheduled job succeeded.", StartupExit.JobSucceededMessage);
        string alert = File.ReadAllText(Path.Combine(RepositoryRoot.Find(), "infra", "modules", "job-alerts.bicep"));
        Assert.Contains($"has \"{StartupExit.JobSucceededMessage.TrimEnd('.')}\"", alert, StringComparison.Ordinal);
    }

    [Fact]
    public void JobAlert_ExpectsEachJobByItsContainerName()
    {
        // The alert's Job values are container names: renaming a container without the query would page every day.
        string modules = Path.Combine(RepositoryRoot.Find(), "infra", "modules");
        string alert = File.ReadAllText(Path.Combine(modules, "job-alerts.bicep"));

        Assert.Contains("name: 'purge'", File.ReadAllText(Path.Combine(modules, "purge-job.bicep")), StringComparison.Ordinal);
        Assert.Contains("name: 'research-refresh'",
            File.ReadAllText(Path.Combine(modules, "research-refresh-job.bicep")), StringComparison.Ordinal);
        Assert.Contains("datatable(Job: string) [\"purge\", \"research-refresh\"]", alert, StringComparison.Ordinal);
        Assert.Contains("ContainerName_s in (\"purge\", \"research-refresh\")", alert, StringComparison.Ordinal);
    }
}
