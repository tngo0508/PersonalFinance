using PersonalFinance.Shared.DTOs.Admin;

namespace PersonalFinance.ApiService.Features.Admin;

/// <summary>
/// Provides system-level administrative metrics and role management.
/// </summary>
public interface IAdminService
{
    Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<List<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<List<AdminRoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<AdminOperationResult> CreateRoleAsync(string roleName);

    Task<AdminOperationResult> DeleteRoleAsync(string roleName);

    Task<AdminOperationResult> AssignRoleAsync(string userId, string roleName);

    Task<AdminOperationResult> RemoveRoleAsync(string actingUserId, string userId, string roleName);
}
