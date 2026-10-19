using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// Admin-only API endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AppPolicies.RequireAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// Gets system-level account counts.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        return Ok(await _adminService.GetSummaryAsync(cancellationToken));
    }
}
