namespace PersonalFinance.ApiService.Features.AccountLifecycle;

/// <summary>
/// The outcome of one run of <see cref="IAccountPurgeService.PurgeDueAsync"/>: counts only, never Account details.
/// </summary>
/// <param name="Purged">Accounts erased.</param>
/// <param name="Skipped">Requests cancelled or purged by someone else while this run worked on them.</param>
/// <param name="Failed">Accounts that could not be erased; they are retried on the next run.</param>
public sealed record AccountPurgeSummary(int Purged, int Skipped, int Failed);
