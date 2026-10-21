using PersonalFinance.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for Dashboard endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Gets a summary of all dashboard data for the current user.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var summary = await _dashboardService.GetDashboardSummaryAsync(userId, cancellationToken);
        return Ok(summary);
    }
}
