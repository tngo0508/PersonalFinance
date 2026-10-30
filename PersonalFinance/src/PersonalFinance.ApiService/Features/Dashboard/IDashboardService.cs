using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Dashboard;

namespace PersonalFinance.ApiService.Features.Dashboard;

/// <summary>
/// Service for aggregating dashboard data from all features.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets a summary of all dashboard data for a user; the Atelier card's titles are in <paramref name="language"/>
    /// (English when null).
    /// </summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId, string? language = null,
        CancellationToken cancellationToken = default);
}
