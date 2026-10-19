using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for admin-only pages. Non-admins receive 404.
/// </summary>
[Authorize(Policy = AppPolicies.RequireAdmin)]
public class AdminController : Controller
{
    private readonly IAdminApi _adminApi;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminApi adminApi, ILogger<AdminController> logger)
    {
        _adminApi = adminApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Admin Dashboard with system-level counts.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new AdminDashboardViewModel { AppVersion = AppVersion.Current };

        try
        {
            model.Summary = await _adminApi.GetSummaryAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the admin summary.");
            model.LoadFailed = true;
        }

        return View(model);
    }
}
