using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Dashboard home page.
/// Calls IDashboardApi to aggregate data from all features.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardApi _dashboardApi;

    public DashboardController(IDashboardApi dashboardApi)
    {
        _dashboardApi = dashboardApi;
    }

    /// <summary>
    /// Displays the Dashboard home page with financial overview.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _dashboardApi.GetSummaryAsync(cancellationToken);
            return View(summary);
        }
        catch
        {
            // Return empty model on error so the page still renders
            return View(new DashboardSummaryDto());
        }
    }
}