using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for Dashboard endpoints.
/// </summary>
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

    private string GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId))
        {
            var value = userId.First();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return "anonymous";
    }

    /// <summary>
    /// Gets a summary of all dashboard data for the current user.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var summary = await _dashboardService.GetDashboardSummaryAsync(userId, cancellationToken);
        return Ok(summary);
    }
}
