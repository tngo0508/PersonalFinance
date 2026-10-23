namespace PersonalFinance.Data.Entities;

/// <summary>
/// An Account's Deletion Request (ADR 0005). Unlike user-owned data it is not an <see cref="AuditableEntity"/>: it is
/// never soft-deleted, and it is the one record that outlives the Purge, keeping only when the request was made and
/// when it was purged.
/// </summary>
public class DeletionRequest
{
    public int Id { get; set; }

    /// <summary>
    /// The Account that asked to be erased.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// When the request was confirmed (UTC). It is also the <c>DeletedAt</c> stamp on every row the request hid, so
    /// a cancel restores exactly those rows.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When the Grace Period ends and the Account may be purged (UTC).
    /// </summary>
    public DateTime PurgeAfter { get; set; }

    public DeletionRequestStatus Status { get; set; } = DeletionRequestStatus.Pending;

    /// <summary>
    /// SHA-256 hash (hex) of the single-use cancel token emailed to the Account; the token itself is never stored.
    /// </summary>
    public string CancelTokenHash { get; set; } = string.Empty;

    public DateTime? CancelledAt { get; set; }

    public DateTime? PurgedAt { get; set; }
}

/// <summary>
/// Where a <see cref="DeletionRequest"/> is in its life.
/// </summary>
public enum DeletionRequestStatus
{
    /// <summary>In its Grace Period: data hidden, sign-in blocked, cancellable.</summary>
    Pending = 1,

    /// <summary>Cancelled from the email link; the Account and its data were restored.</summary>
    Cancelled = 2,

    /// <summary>The Purge erased the Account.</summary>
    Purged = 3
}
