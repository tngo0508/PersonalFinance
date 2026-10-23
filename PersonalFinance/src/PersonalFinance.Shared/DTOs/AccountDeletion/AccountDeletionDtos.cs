namespace PersonalFinance.Shared.DTOs.AccountDeletion;

/// <summary>
/// A Deletion Request the ApiService accepted. The cancel token is returned once, so Web can email the cancel link;
/// it is never stored or shown again.
/// </summary>
public class DeletionRequestAcceptedDto
{
    /// <summary>The single-use cancel token for the confirmation email.</summary>
    public string CancelToken { get; set; } = string.Empty;

    /// <summary>When the Grace Period ends and the Account may be purged (UTC).</summary>
    public DateTime PurgeAfterUtc { get; set; }
}

/// <summary>
/// Cancels a Deletion Request with the token from the confirmation email.
/// </summary>
public class CancelDeletionRequest
{
    public string Token { get; set; } = string.Empty;
}
