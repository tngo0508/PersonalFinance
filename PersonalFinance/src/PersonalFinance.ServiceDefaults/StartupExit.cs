namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Process exit codes for Web and ApiService <c>Program.cs</c>. A startup failure must exit non-zero so
/// <c>--migrate-only</c> jobs, containers and CI can tell a crash from a clean shutdown.
/// </summary>
public static class StartupExit
{
    /// <summary>Clean shutdown, or a successful <c>--migrate-only</c> run.</summary>
    public const int Success = 0;

    /// <summary>An unhandled exception reached the top-level <c>catch</c>.</summary>
    public const int Failure = 1;

    /// <summary>
    /// Logged by a scheduled Container Apps Job (the Purge, the Research refresh) only on the path that returns
    /// <see cref="Success"/>. The job alert (<c>infra/modules/job-alerts.bicep</c>) fires when a job logs no line
    /// containing it for 26 hours. It must stay free of message-template properties: Serilog's console theme wraps
    /// property values in ANSI codes, which would break the match.
    /// </summary>
    public const string JobSucceededMessage = "Scheduled job succeeded.";

    /// <summary>
    /// True when the exception is a deliberate stop rather than a crash: <see cref="HostAbortedException"/>
    /// (EF design-time tooling such as <c>dotnet ef</c> aborts the host after it is built), or an
    /// <see cref="OperationCanceledException"/> once the host has started stopping. A cancellation before that,
    /// such as a startup timeout, is still a failure.
    /// </summary>
    public static bool IsShutdown(Exception exception, CancellationToken applicationStopping) =>
        exception is HostAbortedException ||
        (exception is OperationCanceledException && applicationStopping.IsCancellationRequested);
}
