using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service interface for querying Google Drive folder contents.
/// </summary>
public interface IGoogleDriveService
{
    /// <summary>
    /// Fetches all files within the given Google Drive folder URL or ID (without persisting to DB).
    /// </summary>
    Task<GoogleDriveFolderResponseDto> GetFolderFilesAsync(GoogleDriveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all saved Google Drive connections for an authenticated user.
    /// </summary>
    Task<List<GoogleDriveConnectionDto>> GetUserConnectionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files for a connection from database cache (or syncs if stale/forced).
    /// </summary>
    Task<GoogleDriveFolderResponseDto> GetConnectionFilesAsync(int connectionId, string userId, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects and persists a new or existing Google Drive folder connection.
    /// </summary>
    Task<GoogleDriveConnectionDto> ConnectDriveAsync(ConnectGoogleDriveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a connected Google Drive folder with database cache incrementally.
    /// </summary>
    Task<GoogleDriveFolderResponseDto> SyncConnectionAsync(int connectionId, string userId, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates credentials and re-authenticates a connection.
    /// </summary>
    Task<GoogleDriveConnectionDto> ReauthConnectionAsync(int connectionId, string userId, string? newApiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a connection and all its cached data from database.
    /// </summary>
    Task<bool> DeleteConnectionAsync(int connectionId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and parses a spreadsheet from Google Drive (or database cache / sample data) into a monthly budget report with actual data.
    /// </summary>
    Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
