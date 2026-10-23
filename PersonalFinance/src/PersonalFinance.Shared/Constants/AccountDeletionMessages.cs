namespace PersonalFinance.Shared.Constants;

/// <summary>
/// User-facing messages for Deletion Requests, shared by ApiService and Web.
/// </summary>
public static class AccountDeletionMessages
{
    /// <summary>Why the last Admin cannot make a Deletion Request.</summary>
    public const string LastAdmin =
        "You are the only Admin, so your account cannot be deleted. Make another account an Admin first.";

    /// <summary>Why a second Deletion Request is refused.</summary>
    public const string AlreadyPending = "Your account is already scheduled for deletion.";

    /// <summary>Why a cancel link did nothing.</summary>
    public const string InvalidCancelLink =
        "This link is not valid. It may have been used already, or the deletion may already be final.";
}
