namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Request payload to re-authenticate or update credentials for a Google Drive connection.
/// </summary>
public class ReauthGoogleDriveRequestDto
{
    public int ConnectionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? ApiKey { get; set; }
}
