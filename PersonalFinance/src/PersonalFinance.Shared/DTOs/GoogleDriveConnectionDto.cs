namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Data Transfer Object representing a user's connected Google Drive configuration.
/// </summary>
public class GoogleDriveConnectionDto
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = "Google Drive";

    public string FolderId { get; set; } = string.Empty;

    public string FolderUrl { get; set; } = string.Empty;

    public string? MaskedApiKey { get; set; }

    public bool HasApiKey => !string.IsNullOrEmpty(MaskedApiKey);

    public bool IsValid { get; set; } = true;

    public string SyncStatus { get; set; } = "Pending";

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastSyncedAtUtc { get; set; }

    public int CachedFilesCount { get; set; }

    public long TotalSizeBytes { get; set; }

    public string TotalSizeFormatted { get; set; } = "0 B";

    public bool IsStale => LastSyncedAtUtc == null || (DateTime.UtcNow - LastSyncedAtUtc.Value) > TimeSpan.FromMinutes(30);
}
