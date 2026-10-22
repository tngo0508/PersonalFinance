using Microsoft.Extensions.Hosting;
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
}
