namespace PersonalFinance.Shared.Constants;

/// <summary>
/// What remains of a purged Account (ADR 0005): the placeholder id its kept Guild Hall stories and comments and its
/// Deletion Request hold instead of the Account's id. Those posts have no Pseudonym any more, so the Guild Hall shows
/// them as written by "Former member" (ADR 0018).
/// </summary>
public static class DeletedAccount
{
    /// <summary>Placeholder Account id. Identity ids are GUIDs, so it matches no Account.</summary>
    public const string UserId = "deleted-account";
}
