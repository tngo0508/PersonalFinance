namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Owns Deletion Requests (ADR 0005): making one hides the Account's data and blocks sign-in for the Grace Period;
/// cancelling one restores exactly what it hid.
/// </summary>
public interface IAccountDeletionService
{
    /// <summary>
    /// Makes a Deletion Request for <paramref name="userId"/>. The caller has already confirmed the password (or
    /// the typed confirmation). In one save: every live user-owned row is soft-deleted with one shared stamp, the
    /// Account is locked out with a new security stamp, and the request is stored with the hash of a new cancel token.
    /// </summary>
    Task<DeletionRequestResult> RequestAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the pending, unexpired Deletion Request whose cancel token is <paramref name="token"/>: restores the
    /// rows with its stamp, ends the lockout and marks the request cancelled, so the token works once. Returns false
    /// for an unknown, used or expired token.
    /// </summary>
    Task<bool> CancelAsync(string token, CancellationToken cancellationToken = default);
}
