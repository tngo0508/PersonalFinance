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

    /// <summary>
    /// Lists all accounts with their roles.
    /// </summary>
    [Get("/api/admin/users")]
    Task<List<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all roles with member counts.
    /// </summary>
    [Get("/api/admin/roles")]
    Task<List<AdminRoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new role.
    /// </summary>
    [Post("/api/admin/roles")]
    Task CreateRoleAsync([Body] CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a role that is not built in and has no members.
    /// </summary>
    [Delete("/api/admin/roles/{roleName}")]
    Task DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to an account.
    /// </summary>
    [Put("/api/admin/users/{userId}/roles/{roleName}")]
    Task AssignRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from an account.
    /// </summary>
    [Delete("/api/admin/users/{userId}/roles/{roleName}")]
    Task RemoveRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
}
