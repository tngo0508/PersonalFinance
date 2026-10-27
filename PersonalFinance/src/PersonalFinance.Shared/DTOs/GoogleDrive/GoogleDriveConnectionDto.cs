using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.Shared.DTOs.GoogleDrive;

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

    /// <summary>The ISO 4217 currency of the folder's spreadsheets, chosen at connect time; imported rows inherit it.</summary>
    public string CurrencyCode { get; set; } = "USD";

    public string? MaskedApiKey { get; set; }

    public bool HasApiKey => !string.IsNullOrEmpty(MaskedApiKey);

    public bool IsValid { get; set; } = true;

    public string SyncStatus { get; set; } = "Pending";

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// <see cref="ErrorMessage"/> keyed, so the Web app can show it in the reader's language (#405). Null when
    /// <see cref="ErrorMessage"/> is null or has no key.
    /// </summary>
    public ApiErrorEntryDto? Error { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastSyncedAtUtc { get; set; }

    public int CachedFilesCount { get; set; }

    public long TotalSizeBytes { get; set; }

    public string TotalSizeFormatted { get; set; } = "0 B";

    public bool IsStale => LastSyncedAtUtc == null || (DateTime.UtcNow - LastSyncedAtUtc.Value) > TimeSpan.FromMinutes(30);
}
