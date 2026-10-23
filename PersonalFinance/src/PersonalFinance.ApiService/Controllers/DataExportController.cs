using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs.DataExport;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for the Data Export, scoped to the authenticated Account (see XUserIdAuthenticationHandler).
/// </summary>
[Authorize]
[ApiController]
[Route("api/data-export")]
[Produces("application/json")]
public class DataExportController : ControllerBase
{
    private readonly IDataExportService _dataExportService;

    public DataExportController(IDataExportService dataExportService)
    {
        _dataExportService = dataExportService;
    }

    /// <summary>
    /// Returns the current Account's Data Export: every live record it holds, never another Account's.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DataExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken = default)
    {
        return Ok(await _dataExportService.ExportAsync(User.GetUserId(), cancellationToken));
    }
}
