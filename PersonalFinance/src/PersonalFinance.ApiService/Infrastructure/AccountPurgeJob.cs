using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The <c>--purge-only</c> mode (or <c>PURGE_ONLY=true</c>): the ApiService image run by the daily Container Apps Job
/// (<c>infra/modules/purge-job.bicep</c>, ADR 0005). It applies pending migrations, purges every due Deletion
/// Request and exits, without starting the web server. The ApiService itself scales to zero, so it cannot be relied
/// on to run the Purge every day.
/// </summary>
public static class AccountPurgeJob
{
    public const string Argument = "--purge-only";

    /// <summary>True when the process was started to run the Purge and exit.</summary>
    public static bool IsRequested(string[] args) =>
        args.Contains(Argument) ||
        string.Equals(Environment.GetEnvironmentVariable("PURGE_ONLY"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Migrates, then purges. Returns the process exit code: non-zero when any Account failed, so the job execution
    /// shows as failed while the other Accounts are still erased (and the failed ones retried on the next run).
    /// </summary>
    public static async Task<int> RunAsync(IHost host, CancellationToken cancellationToken = default)
    {
        await host.MigrateAndSeedDatabaseAsync(cancellationToken);

        await using var scope = host.Services.CreateAsyncScope();
        var summary = await scope.ServiceProvider.GetRequiredService<IAccountPurgeService>()
            .PurgeDueAsync(cancellationToken);

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AccountPurgeJob));
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Purge finished: {Purged} purged, {Skipped} skipped, {Failed} failed.",
                summary.Purged, summary.Skipped, summary.Failed);
        }

        return summary.Failed == 0 ? StartupExit.Success : StartupExit.Failure;
    }
}
