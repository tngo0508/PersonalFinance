namespace PersonalFinance.ApiService.Features.AccountLifecycle;

/// <summary>
/// The outcome of <see cref="IAccountDeletionService.RequestAsync"/>.
/// </summary>
public sealed record DeletionRequestResult(DeletionRequestOutcome Outcome, string? CancelToken, DateTime? PurgeAfter)
{
    public static DeletionRequestResult Accepted(string cancelToken, DateTime purgeAfter) =>
        new(DeletionRequestOutcome.Accepted, cancelToken, purgeAfter);

    public static DeletionRequestResult Refused(DeletionRequestOutcome outcome) => new(outcome, null, null);
}

public enum DeletionRequestOutcome
{
    Accepted,

    /// <summary>The Account is the last Admin.</summary>
    LastAdmin,

    /// <summary>No such Account.</summary>
    AccountNotFound,

    /// <summary>The Account already has a pending Deletion Request.</summary>
    AlreadyPending
}
