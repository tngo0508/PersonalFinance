using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service interface for querying Google Drive folder contents.
/// </summary>
public interface IGoogleDriveService
{
    /// <summary>
    /// Fetches all files within the given Google Drive folder URL or ID.
    /// </summary>
    Task<GoogleDriveFolderResponseDto> GetFolderFilesAsync(GoogleDriveRequestDto request, CancellationToken cancellationToken = default);
}
