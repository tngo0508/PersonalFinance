using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Admin;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Admin;
using ApiException = Refit.ApiException;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for admin-only pages. Non-admins receive 404.
/// </summary>
[Authorize(Policy = AppPolicies.RequireAdmin)]
public class AdminController : Controller
{
    private readonly IAdminApi _adminApi;
    private readonly IFeedbackApi _feedbackApi;
    private readonly ILogger<AdminController> _logger;
    private readonly IStringLocalizer _localizer;

    /// <param name="localizer">
    /// Resolves ApiService's keyed error messages; the Admin pages themselves are not localized.
    /// </param>
    public AdminController(IAdminApi adminApi, IFeedbackApi feedbackApi, ILogger<AdminController> logger,
        IStringLocalizer localizer)
    {
        _adminApi = adminApi;
        _feedbackApi = feedbackApi;
        _logger = logger;
        _localizer = localizer;
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
    /// Displays the Feedback inbox, newest first, filtered to <paramref name="status"/> (by default New and
    /// Reviewed; Archived is hidden). A failed load shows the Unavailable State, never an empty inbox.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Feedback(FeedbackStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<FeedbackAdminItemDto> items = await _feedbackApi.ListAsync(status, cancellationToken);
            return View(new AdminFeedbackInboxViewModel { Items = items, Status = status });
        }
        catch (Exception ex) when ((ex is ApiException or HttpRequestException or TaskCanceledException)
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the Feedback inbox.");
            Dictionary<string, string> retryRouteValues = status is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["status"] = status.Value.ToString() };
            return this.UnavailableView("Feedback inbox", "We couldn't load the Feedback inbox", "Admin",
                nameof(Feedback), retryRouteValues);
        }
    }

    /// <summary>
    /// Sets the Feedback Status of one piece of Feedback, then returns to the inbox with the
    /// <paramref name="filter"/> the Admin was viewing.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeFeedbackStatus(int id, FeedbackStatus status, FeedbackStatus? filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _feedbackApi.ChangeStatusAsync(id, new ChangeFeedbackStatusRequest { Status = status },
                cancellationToken);
            TempData["AdminMessage"] = $"Feedback marked {status}.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            TempData["AdminError"] = "That Feedback no longer exists.";
        }
        catch (Exception ex) when ((ex is ApiException or HttpRequestException or TaskCanceledException)
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to change the Feedback Status.");
            TempData["AdminError"] = ApiErrorMessages.ForUser(_localizer, ex, "The status could not be changed. Try again later.");
        }

        return RedirectToAction(nameof(Feedback), new { status = filter });
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
            TempData["AdminError"] = ApiErrorMessages.ForUser(_localizer, ex, "The operation failed. Try again later.");
        }

        return RedirectToAction(nameof(Index));
    }
}
