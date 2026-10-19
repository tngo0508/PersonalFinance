using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Admin;

namespace PersonalFinance.ApiService.Services;

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

public class AdminService : IAdminService
{
    private const int MaxRoleNameLength = 50;

    // Serializes the "count Admins, then remove" check so concurrent removals cannot leave zero Admins.
    private static readonly SemaphoreSlim AdminRemovalLock = new(1, 1);

    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminService(
        AppDbContext db,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var total = await _db.Users.AsNoTracking().CountAsync(cancellationToken);
        var confirmed = await _db.Users.AsNoTracking().CountAsync(u => u.EmailConfirmed, cancellationToken);

        var perRole = await (
                from ur in _db.UserRoles.AsNoTracking()
                join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                group ur by r.Name
                into g
                orderby g.Key
                select new { Role = g.Key ?? string.Empty, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new AdminSummaryDto
        {
            TotalAccounts = total,
            ConfirmedAccounts = confirmed,
            UnconfirmedAccounts = total - confirmed,
            AccountsPerRole = perRole.ToDictionary(x => x.Role, x => x.Count)
        };
    }

    public async Task<List<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.Email)
            .ThenBy(u => u.Id)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? string.Empty,
                EmailConfirmed = u.EmailConfirmed
            })
            .ToListAsync(cancellationToken);

        var assignments = await (
                from ur in _db.UserRoles.AsNoTracking()
                join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                orderby r.Name
                select new { ur.UserId, RoleName = r.Name ?? string.Empty })
            .ToListAsync(cancellationToken);

        var byUser = assignments
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.RoleName).ToList());

        foreach (var user in users)
        {
            if (byUser.TryGetValue(user.Id, out var roles))
            {
                user.Roles = roles;
            }
        }

        return users;
    }

    public async Task<List<AdminRoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await (
                from r in _db.Roles.AsNoTracking()
                orderby r.Name
                select new
                {
                    Name = r.Name ?? string.Empty,
                    Count = _db.UserRoles.Count(ur => ur.RoleId == r.Id)
                })
            .ToListAsync(cancellationToken);

        return roles
            .Select(r => new AdminRoleDto
            {
                Name = r.Name,
                UserCount = r.Count,
                IsBuiltIn = AppRoles.All.Contains(r.Name)
            })
            .ToList();
    }

    public async Task<AdminOperationResult> CreateRoleAsync(string roleName)
    {
        var name = (roleName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxRoleNameLength)
        {
            return AdminOperationResult.Rejected($"Role name must be between 1 and {MaxRoleNameLength} characters.");
        }

        if (!name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
        {
            return AdminOperationResult.Rejected("Role name may only contain letters, digits, '-' and '_'.");
        }

        if (await _roleManager.RoleExistsAsync(name))
        {
            return AdminOperationResult.Rejected($"Role '{name}' already exists.");
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(name));
        return result.Succeeded
            ? AdminOperationResult.Ok()
            : AdminOperationResult.Rejected("The role could not be created.");
    }

    public async Task<AdminOperationResult> DeleteRoleAsync(string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return AdminOperationResult.NotFound($"Role '{roleName}' was not found.");
        }

        if (AppRoles.All.Contains(role.Name ?? string.Empty))
        {
            return AdminOperationResult.Rejected($"Built-in role '{role.Name}' cannot be deleted.");
        }

        var members = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (members.Count > 0)
        {
            return AdminOperationResult.Rejected($"Role '{role.Name}' still has {members.Count} account(s). Remove them first.");
        }

        var result = await _roleManager.DeleteAsync(role);
        return result.Succeeded
            ? AdminOperationResult.Ok()
            : AdminOperationResult.Rejected("The role could not be deleted.");
    }

    public async Task<AdminOperationResult> AssignRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AdminOperationResult.NotFound("Account not found.");
        }

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return AdminOperationResult.NotFound($"Role '{roleName}' was not found.");
        }

        if (await _userManager.IsInRoleAsync(user, role.Name!))
        {
            return AdminOperationResult.Ok();
        }

        var result = await _userManager.AddToRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            return AdminOperationResult.Rejected("The role could not be assigned.");
        }

        // Makes the Web cookie pick up the new role at the next security-stamp check.
        await _userManager.UpdateSecurityStampAsync(user);
        return AdminOperationResult.Ok();
    }

    public async Task<AdminOperationResult> RemoveRoleAsync(string actingUserId, string userId, string roleName)
    {
        await AdminRemovalLock.WaitAsync();
        try
        {
            return await RemoveRoleCoreAsync(actingUserId, userId, roleName);
        }
        finally
        {
            AdminRemovalLock.Release();
        }
    }

    private async Task<AdminOperationResult> RemoveRoleCoreAsync(string actingUserId, string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AdminOperationResult.NotFound("Account not found.");
        }

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return AdminOperationResult.NotFound($"Role '{roleName}' was not found.");
        }

        if (!await _userManager.IsInRoleAsync(user, role.Name!))
        {
            return AdminOperationResult.Ok();
        }

        if (role.Name == AppRoles.Admin)
        {
            if (string.Equals(actingUserId, userId, StringComparison.Ordinal))
            {
                return AdminOperationResult.Rejected("You cannot remove the Admin role from your own account.");
            }

            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            if (admins.Count <= 1)
            {
                return AdminOperationResult.Rejected("The last Admin cannot be removed.");
            }
        }

        var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            return AdminOperationResult.Rejected("The role could not be removed.");
        }

        await _userManager.UpdateSecurityStampAsync(user);
        return AdminOperationResult.Ok();
    }
}
