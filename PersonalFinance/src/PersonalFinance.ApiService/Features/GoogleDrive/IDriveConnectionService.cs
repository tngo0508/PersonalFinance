using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.GoogleDrive;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// Service interface for managing Google Drive connections: explore, connect, sync, re-auth, and delete.
/// </summary>
public interface IDriveConnectionService
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
    /// Connects and persists a new or existing Google Drive folder connection owned by <paramref name="userId"/>.
    /// </summary>
    Task<GoogleDriveConnectionDto> ConnectDriveAsync(string userId, ConnectGoogleDriveRequestDto request, CancellationToken cancellationToken = default);

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
}
