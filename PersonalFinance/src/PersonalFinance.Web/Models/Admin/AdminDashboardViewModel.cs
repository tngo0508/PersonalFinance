using PersonalFinance.Shared.DTOs.Admin;

namespace PersonalFinance.Web.Models.Admin;

/// <summary>
/// View model for the Admin Dashboard page.
/// </summary>
public class AdminDashboardViewModel
{
    public AdminSummaryDto Summary { get; set; } = new();

    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// True when the summary could not be loaded from the API.
    /// </summary>
    public bool LoadFailed { get; set; }

    public List<AdminUserDto> Users { get; set; } = new();

    public List<AdminRoleDto> Roles { get; set; } = new();

    /// <summary>
    /// Id of the signed-in admin, used to prevent self-demotion in the UI.
    /// </summary>
    public string CurrentUserId { get; set; } = string.Empty;
}
