using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Admin;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Provides system-level administrative metrics.
/// </summary>
public interface IAdminService
{
    Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db)
    {
        _db = db;
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
}
