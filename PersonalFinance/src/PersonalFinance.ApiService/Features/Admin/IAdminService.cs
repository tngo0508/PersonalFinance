using PersonalFinance.Shared.DTOs.Admin;

namespace PersonalFinance.ApiService.Features.Admin;

/// <summary>
/// Outcome of an admin role-management operation.
/// </summary>
public enum AdminOperationStatus
{
    Success,
    NotFound,
    Rejected
}

/// <summary>
/// Result of an admin role-management operation; <see cref="Error"/> is set when it was not successful.
/// </summary>
public record AdminOperationResult(AdminOperationStatus Status, string? Error = null)
{
    public bool Succeeded => Status == AdminOperationStatus.Success;

    public static AdminOperationResult Ok() => new(AdminOperationStatus.Success);

    public static AdminOperationResult NotFound(string error) => new(AdminOperationStatus.NotFound, error);

    public static AdminOperationResult Rejected(string error) => new(AdminOperationStatus.Rejected, error);
}

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
