using PersonalFinance.Shared.DTOs.Admin;

namespace PersonalFinance.Web.Models;

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
}
