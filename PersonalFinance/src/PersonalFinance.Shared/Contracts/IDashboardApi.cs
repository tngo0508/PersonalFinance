using PersonalFinance.Shared.DTOs.Dashboard;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Dashboard operations.
/// </summary>
public interface IDashboardApi
{
    /// <summary>
    /// Gets a summary of all dashboard data for the current user; the Atelier card's titles are in
    /// <paramref name="language"/> (English when null).
    /// </summary>
    [Get("/api/Dashboard/summary")]
    Task<DashboardSummaryDto> GetSummaryAsync([Query] string? language = null,
        CancellationToken cancellationToken = default);
}
