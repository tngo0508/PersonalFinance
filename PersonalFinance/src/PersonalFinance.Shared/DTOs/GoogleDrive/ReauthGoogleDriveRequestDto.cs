namespace PersonalFinance.Shared.DTOs.GoogleDrive;

/// <summary>
/// Request payload to re-authenticate or update credentials for a Google Drive connection.
/// </summary>
public class ReauthGoogleDriveRequestDto
{
    public int ConnectionId { get; set; }

    public string? ApiKey { get; set; }
}
