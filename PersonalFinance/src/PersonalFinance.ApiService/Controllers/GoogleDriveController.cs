using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// RESTful API controller for exploring Google Drive folders and retrieving file metadata.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GoogleDriveController : ControllerBase
{
    private readonly IGoogleDriveService _googleDriveService;
    private readonly ILogger<GoogleDriveController> _logger;

    public GoogleDriveController(
        IGoogleDriveService googleDriveService,
        ILogger<GoogleDriveController> logger)
    {
        _googleDriveService = googleDriveService;
        _logger = logger;
    }

    /// <summary>
    /// Explores a Google Drive folder and returns the list of files.
    /// </summary>
    [HttpPost("explore")]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GoogleDriveFolderResponseDto>> Explore(
        [FromBody] GoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.FolderUrl))
        {
            return BadRequest(new GoogleDriveFolderResponseDto
            {
                Success = false,
                ErrorMessage = "Please provide a valid Google Drive folder URL or Folder ID."
            });
        }

        _logger.LogInformation("Processing Google Drive explore request for input '{Url}'", request.FolderUrl);

        var result = await _googleDriveService.GetFolderFilesAsync(request, cancellationToken);
        if (!result.Success && string.IsNullOrEmpty(result.FolderId))
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
