using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Dashboard;

namespace PersonalFinance.ApiService.Features.Dashboard;

/// <summary>
/// Service for aggregating dashboard data from all features.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets a summary of all dashboard data for a user.
    /// </summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId, CancellationToken cancellationToken = default);
}
