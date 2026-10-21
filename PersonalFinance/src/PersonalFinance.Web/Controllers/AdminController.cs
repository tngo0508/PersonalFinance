using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Admin;
using PersonalFinance.Web.Models;
using ApiException = Refit.ApiException;
using PersonalFinance.Web.Infrastructure;

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
        var model = new AdminDashboardViewModel
        {
            AppVersion = AppVersion.Current,
            CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty
        };

        try
        {
            model.Summary = await _adminApi.GetSummaryAsync(cancellationToken);
            model.Users = await _adminApi.GetUsersAsync(cancellationToken);
            model.Roles = await _adminApi.GetRolesAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the admin summary.");
            model.LoadFailed = true;
        }

        return View(model);
    }

    /// <summary>
    /// Assigns a role to an account.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AssignRole(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        return RunAsync(
            () => _adminApi.AssignRoleAsync(userId, roleName, cancellationToken),
            $"Role '{roleName}' assigned.");
    }

    /// <summary>
    /// Removes a role from an account.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveRole(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        return RunAsync(
            () => _adminApi.RemoveRoleAsync(userId, roleName, cancellationToken),
            $"Role '{roleName}' removed.");
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateRole(string roleName, CancellationToken cancellationToken = default)
    {
        return RunAsync(
            () => _adminApi.CreateRoleAsync(new CreateRoleRequest { Name = roleName ?? string.Empty }, cancellationToken),
            $"Role '{roleName?.Trim()}' created.");
    }

    /// <summary>
    /// Deletes a role that is not built in and has no members.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteRole(string roleName, CancellationToken cancellationToken = default)
    {
        return RunAsync(
            () => _adminApi.DeleteRoleAsync(roleName, cancellationToken),
            $"Role '{roleName}' deleted.");
    }

    private async Task<IActionResult> RunAsync(Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            TempData["AdminMessage"] = successMessage;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Admin role operation failed.");
            TempData["AdminError"] = ApiErrorMessages.ForUser(ex, "The operation failed. Try again later.");
        }

        return RedirectToAction(nameof(Index));
    }
}
