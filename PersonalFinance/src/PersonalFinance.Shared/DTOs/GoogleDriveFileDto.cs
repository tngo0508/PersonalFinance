namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Represents a file or subfolder metadata item within a Google Drive folder.
/// </summary>
public class GoogleDriveFileDto
{
    /// <summary>
    /// Google Drive file ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Name of the file or subfolder including extension.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MIME type returned by Google Drive (e.g., application/pdf, image/png, application/vnd.google-apps.spreadsheet).
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes (null for native Google Workspace docs or folders).
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Human-readable formatted file size (e.g. 1.5 MB, 320 KB, or "-" for folders/docs).
    /// </summary>
    public string SizeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the file was created.
    /// </summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// UTC timestamp when the file was last modified.
    /// </summary>
    public DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// URL to open and preview the file directly in Google Drive.
    /// </summary>
    public string? WebViewLink { get; set; }

    /// <summary>
    /// URL to an icon representing the file type.
    /// </summary>
    public string? IconLink { get; set; }

    /// <summary>
    /// URL to a thumbnail preview of the file if available.
    /// </summary>
    public string? ThumbnailLink { get; set; }

    /// <summary>
    /// Friendly file category description (e.g., PDF Document, Google Spreadsheet, JPEG Image, Folder).
    /// </summary>
    public string FileType { get; set; } = "Unknown";

    /// <summary>
    /// Bootstrap or CSS icon class for rendering.
    /// </summary>
    public string IconBadgeClass { get; set; } = "bg-secondary";

    /// <summary>
    /// Indicates whether this item is a directory/folder.
    /// </summary>
    public bool IsFolder { get; set; }
}
