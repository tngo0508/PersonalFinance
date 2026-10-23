using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Dashboard;

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
        string userId = User.GetUserId();
        DashboardSummaryDto summary = await _dashboardService.GetDashboardSummaryAsync(userId, cancellationToken);
        return Ok(summary);
    }
}
