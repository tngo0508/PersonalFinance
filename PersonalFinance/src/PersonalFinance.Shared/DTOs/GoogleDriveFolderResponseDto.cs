namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Response payload containing the metadata and files of an explored Google Drive folder.
/// </summary>
public class GoogleDriveFolderResponseDto
{
    /// <summary>
    /// Extracted Google Drive folder ID.
    /// </summary>
    public string FolderId { get; set; } = string.Empty;

    /// <summary>
    /// Original URL or input supplied by the user.
    /// </summary>
    public string FolderUrl { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the Google Drive folder.
    /// </summary>
    public string? FolderName { get; set; }

    /// <summary>
    /// Total number of files and folders located.
    /// </summary>
    public int TotalFilesCount => Files.Count;

    /// <summary>
    /// Total accumulated size in bytes of files with known sizes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Human-readable total accumulated size (e.g., 42.8 MB).
    /// </summary>
    public string TotalSizeFormatted { get; set; } = "0 B";

    /// <summary>
    /// List of files and folders contained within the folder.
    /// </summary>
    public List<GoogleDriveFileDto> Files { get; set; } = new();

    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Informational or warning message (e.g. guidance when demo mode / missing API key).
    /// </summary>
    public string? WarningMessage { get; set; }

    /// <summary>
    /// Indicates whether the response contains simulated sample/demo data.
    /// </summary>
    public bool IsDemoData { get; set; }
}
