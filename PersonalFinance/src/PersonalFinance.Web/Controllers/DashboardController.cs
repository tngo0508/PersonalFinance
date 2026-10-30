using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Dashboard home page.
/// Calls IDashboardApi to aggregate data from all features.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    /// <summary>View shown when the summary cannot be loaded.</summary>
    public const string UnavailableViewName = "Unavailable";

    private readonly IDashboardApi _dashboardApi;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardApi dashboardApi, ILogger<DashboardController> logger)
    {
        _dashboardApi = dashboardApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Dashboard home page with financial overview.
    /// When the API is unavailable (for example while ApiService is still starting), the page shows the
    /// Unavailable State with a Retry link, never the empty state a user with no data would see.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            DashboardSummaryDto summary = await _dashboardApi.GetSummaryAsync(AppLanguages.Current().Code, cancellationToken);
            return View(summary);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the dashboard summary; rendering the Unavailable State instead.");
            return View(UnavailableViewName);
        }
    }
}
