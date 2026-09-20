namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Request payload to connect and persist a Google Drive folder for an authenticated user.
/// </summary>
public class ConnectGoogleDriveRequestDto
{
    public string UserId { get; set; } = string.Empty;

    public string FolderUrl { get; set; } = string.Empty;

    public string? ConnectionName { get; set; }

    public string? ApiKey { get; set; }
}
