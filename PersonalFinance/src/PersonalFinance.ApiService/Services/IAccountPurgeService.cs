namespace PersonalFinance.ApiService.Services;

/// <summary>
/// The Purge (ADR 0005): permanent erasure of Accounts whose Grace Period has ended.
/// </summary>
public interface IAccountPurgeService
{
    /// <summary>
    /// Purges every pending Deletion Request whose Grace Period has ended, one transaction per Account. Safe to run
    /// twice or on several machines at once: a request cancelled or purged by someone else meanwhile is skipped.
    /// One Account failing does not stop the others.
    /// </summary>
    Task<AccountPurgeSummary> PurgeDueAsync(CancellationToken cancellationToken = default);
}
