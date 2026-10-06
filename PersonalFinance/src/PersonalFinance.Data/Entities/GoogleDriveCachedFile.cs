namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database persistence entity representing a cached Google Drive file or folder in database.
/// </summary>
public class GoogleDriveCachedFile
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key referencing the parent GoogleDriveConnection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Google Drive unique file/folder ID.
    /// </summary>
    public string DriveFileId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the file or subfolder.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MIME type string from Google Drive API.
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes (null for folders / Google Workspace docs).
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Human-readable formatted file size (e.g. 1.2 MB).
    /// </summary>
    public string SizeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp from Google Drive API.
    /// </summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// Last modified timestamp from Google Drive API.
    /// </summary>
    public DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// Web preview/edit link in Google Drive.
    /// </summary>
    public string? WebViewLink { get; set; }

    /// <summary>
    /// Type icon URL.
    /// </summary>
    public string? IconLink { get; set; }

    /// <summary>
    /// Thumbnail image URL.
    /// </summary>
    public string? ThumbnailLink { get; set; }

    /// <summary>
    /// Categorized file type label (e.g. 'Spreadsheet', 'PDF Document').
    /// </summary>
    public string FileType { get; set; } = "Unknown";

    /// <summary>
    /// UI badge CSS class.
    /// </summary>
    public string IconBadgeClass { get; set; } = "bg-secondary";

    /// <summary>
    /// True if item is a folder.
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// True if item is marked as trashed.
    /// </summary>
    public bool IsTrashed { get; set; }

    /// <summary>
    /// Timestamp when this item was fetched and cached locally in database.
    /// </summary>
    public DateTime LastFetchedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to parent connection.
    /// </summary>
    public GoogleDriveConnection Connection { get; set; } = null!;
}
