using PersonalFinance.Shared.DTOs.Admin;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for admin-only operations.
/// </summary>
public interface IAdminApi
{
    /// <summary>
    /// Gets system-level account counts. Requires the Admin role.
    /// </summary>
    [Get("/api/admin/summary")]
    Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
