using PersonalFinance.Shared.DTOs.AccountDeletion;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Deletion Requests (ADR 0005).
/// </summary>
public interface IAccountDeletionApi
{
    /// <summary>
    /// Makes a Deletion Request for the signed-in Account: hides its data, blocks sign-in and starts the Grace
    /// Period. Web confirms the password (or the typed confirmation) first. The last Admin gets 409.
    /// </summary>
    [Post("/api/account-deletion")]
    Task<DeletionRequestAcceptedDto> RequestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending Deletion Request with the token from its email, restoring the Account and the data the
    /// request hid. Needs no signed-in Account; an unknown, used or expired token gets 404.
    /// </summary>
    [Post("/api/account-deletion/cancel")]
    Task CancelAsync([Body] CancelDeletionRequest request, CancellationToken cancellationToken = default);
}
