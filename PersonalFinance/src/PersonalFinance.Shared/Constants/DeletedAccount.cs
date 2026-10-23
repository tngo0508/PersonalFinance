namespace PersonalFinance.Shared.Constants;

/// <summary>
/// What remains of a purged Account (ADR 0005): the author shown on its anonymized Community stories and comments, and
/// the placeholder id those rows and its Deletion Request hold instead of the Account's id.
/// </summary>
public static class DeletedAccount
{
    /// <summary>Placeholder Account id. Identity ids are GUIDs, so it matches no Account.</summary>
    public const string UserId = "deleted-account";

    /// <summary>Author name shown on an anonymized story or comment.</summary>
    public const string DisplayName = "Deleted User";
}
