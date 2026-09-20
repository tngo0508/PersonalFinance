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
}
