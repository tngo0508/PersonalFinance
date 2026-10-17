using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Dashboard;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Dashboard home page.
/// Calls IDashboardApi to aggregate data from all features.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardApi _dashboardApi;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardApi dashboardApi, ILogger<DashboardController> logger)
    {
        _dashboardApi = dashboardApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Dashboard home page with financial overview.
    /// When the API is unavailable, the page still renders with an empty summary so every section shows its empty state.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _dashboardApi.GetSummaryAsync(cancellationToken);
            return View(summary);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the dashboard summary; rendering an empty summary instead.");
            return View(new DashboardSummaryDto());
        }
    }
}
