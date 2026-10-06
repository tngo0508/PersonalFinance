using Refit;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Google Drive folder exploration and file retrieval.
/// </summary>
public interface IGoogleDriveApi
{
    /// <summary>
    /// Explores a Google Drive folder given its link or folder ID and returns its file metadata.
    /// </summary>
    [Post("/api/googledrive/explore")]
    Task<GoogleDriveFolderResponseDto> ExploreFolderAsync([Body] GoogleDriveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved Google Drive connections for an authenticated user.
    /// </summary>
    [Get("/api/googledrive/connections")]
    Task<List<GoogleDriveConnectionDto>> GetUserConnectionsAsync([Query] string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files for a specific connection from local database cache (or synchronizes if stale/forced).
    /// </summary>
    [Get("/api/googledrive/connections/{connectionId}")]
    Task<GoogleDriveFolderResponseDto> GetConnectionFilesAsync(int connectionId, [Query] string userId, [Query] bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects and persists a new or existing Google Drive folder connection.
    /// </summary>
    [Post("/api/googledrive/connections")]
    Task<GoogleDriveConnectionDto> ConnectDriveAsync([Body] ConnectGoogleDriveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually or incrementally triggers synchronization for a specific connection.
    /// </summary>
    [Post("/api/googledrive/connections/{connectionId}/sync")]
    Task<GoogleDriveFolderResponseDto> SyncConnectionAsync(int connectionId, [Body] SyncGoogleDriveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates credentials and re-authenticates a connection with invalid or expired credentials.
    /// </summary>
    [Post("/api/googledrive/connections/{connectionId}/reauth")]
    Task<GoogleDriveConnectionDto> ReauthConnectionAsync(int connectionId, [Body] ReauthGoogleDriveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a connected Drive and deletes all locally cached files from database.
    /// </summary>
    [Delete("/api/googledrive/connections/{connectionId}")]
    Task<bool> DeleteConnectionAsync(int connectionId, [Query] string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and parses a spreadsheet from Google Drive into a monthly budget report with actual data.
    /// </summary>
    [Get("/api/googledrive/spreadsheet-report")]
    Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(
        [Query] string fileId,
        [Query] string? fileName = null,
        [Query] int? connectionId = null,
        [Query] string? userId = null,
        CancellationToken cancellationToken = default);
}
