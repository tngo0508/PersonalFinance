namespace PersonalFinance.Shared.DTOs.GoogleDrive;

/// <summary>
/// Request payload to connect and persist a Google Drive folder.
/// The owning Account is taken from the authenticated request, never from the payload.
/// </summary>
public class ConnectGoogleDriveRequestDto
{
    public string FolderUrl { get; set; } = string.Empty;

    public string? ConnectionName { get; set; }

    public string? ApiKey { get; set; }
}
