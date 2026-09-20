namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Request payload to explore a Google Drive folder.
/// </summary>
public class GoogleDriveRequestDto
{
    /// <summary>
    /// Google Drive folder URL or raw Folder ID.
    /// </summary>
    public string FolderUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional Google Cloud API Key with Google Drive API enabled.
    /// If omitted, the service checks configured application settings.
    /// </summary>
    public string? ApiKey { get; set; }
}
