namespace PersonalFinance.Shared.DTOs.GoogleDrive;

/// <summary>
/// Request payload to synchronize a connected Google Drive folder.
/// </summary>
public class SyncGoogleDriveRequestDto
{
    public int ConnectionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public bool ForceRefresh { get; set; } = true;
}
