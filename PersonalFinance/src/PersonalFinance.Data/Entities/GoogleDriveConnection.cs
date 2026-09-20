namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database persistence entity representing a user's connected Google Drive folder/connection.
/// </summary>
public class GoogleDriveConnection
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key or identifier of the authenticated user who owns this connection.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Friendly display name for the connected Drive/Folder.
    /// </summary>
    public string Name { get; set; } = "Google Drive";

    /// <summary>
    /// Google Drive unique folder ID.
    /// </summary>
    public string FolderId { get; set; } = string.Empty;

    /// <summary>
    /// User supplied folder URL or identifier.
    /// </summary>
    public string FolderUrl { get; set; } = string.Empty;

    /// <summary>
    /// Securely encrypted API key or token for authenticating queries.
    /// </summary>
    public string? EncryptedApiKey { get; set; }

    /// <summary>
    /// Masked representation of the API key for safe UI rendering.
    /// </summary>
    public string? MaskedApiKey { get; set; }

    /// <summary>
    /// Indicates whether the stored credentials/connection are currently valid.
    /// Set to false when Google returns 401/403 requiring re-authentication.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Current synchronization state (e.g. 'Synced', 'Pending', 'Failed', 'NeedsReauth').
    /// </summary>
    public string SyncStatus { get; set; } = "Pending";

    /// <summary>
    /// Optional error message or re-authentication instructions.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when this connection was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when data was last synchronized from Google Drive API.
    /// </summary>
    public DateTime? LastSyncedAtUtc { get; set; }

    /// <summary>
    /// Locally cached files and folders associated with this Drive connection.
    /// </summary>
    public ICollection<GoogleDriveCachedFile> CachedFiles { get; set; } = new List<GoogleDriveCachedFile>();
}
