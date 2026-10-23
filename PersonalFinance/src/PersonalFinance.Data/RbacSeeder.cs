using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Data;

/// <summary>
/// Seeds Identity roles and grants the Admin role to the configured account. Add-only: never removes roles.
/// </summary>
public static class RbacSeeder
{
    /// <summary>
    /// Configuration key holding the email of the account that becomes Admin.
    /// </summary>
    public const string AdminEmailKey = "Rbac:AdminEmail";

    /// <summary>
    /// Ensures every role in <see cref="AppRoles.All"/> exists and grants Admin to the confirmed account matching <c>Rbac:AdminEmail</c>.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Over the ADR 0007 complexity ceiling before it was set; follow-up: split role seeding from the admin grant.")]
    public static async Task SeedAsync(
        IServiceProvider services,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        RoleManager<IdentityRole>? roleManager = services.GetService<RoleManager<IdentityRole>>();
        UserManager<IdentityUser>? userManager = services.GetService<UserManager<IdentityUser>>();
        if (roleManager is null || userManager is null)
        {
            logger?.LogInformation("Identity roles are not registered; skipping RBAC seeding.");
            return;
        }

        bool userRoleCreated = false;
        foreach (string role in AppRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(role))
            {
                IdentityResult created = await roleManager.CreateAsync(new IdentityRole(role));
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{role}': {string.Join(", ", created.Errors.Select(e => e.Code))}");
                }

                if (logger is not null && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Created role {Role}.", role);
                }

                userRoleCreated |= role == AppRoles.User;
            }
        }

        // One-time backfill: only when the User role was just created, so manual removals stay permanent.
        if (userRoleCreated)
        {
            await BackfillDefaultRoleAsync(services, userManager, logger, cancellationToken);
        }

        string? adminEmail = services.GetService<IConfiguration>()?[AdminEmailKey];
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{Key} is not configured; no Admin grant performed.", AdminEmailKey);
            }

            return;
        }

        IdentityUser? user = await userManager.FindByEmailAsync(adminEmail.Trim());
        if (user is null)
        {
            logger?.LogInformation("Configured admin account does not exist yet; restart after it registers.");
            return;
        }

        if (!user.EmailConfirmed)
        {
            logger?.LogWarning("Configured admin account has an unconfirmed email; Admin role not granted.");
            return;
        }

        if (await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return;
        }

        IdentityResult added = await userManager.AddToRoleAsync(user, AppRoles.Admin);
        if (!added.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to grant Admin: {string.Join(", ", added.Errors.Select(e => e.Code))}");
        }

        await userManager.UpdateSecurityStampAsync(user);
        logger?.LogInformation("Granted Admin role to the configured admin account.");
    }

    private static async Task BackfillDefaultRoleAsync(
        IServiceProvider services,
        UserManager<IdentityUser> userManager,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        AppDbContext db = services.GetRequiredService<AppDbContext>();
        string userRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.Name == AppRoles.User)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

        List<string> missingIds = await db.Users.AsNoTracking()
            .Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == userRoleId))
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (string? id in missingIds)
        {
            IdentityUser? account = await userManager.FindByIdAsync(id);
            if (account is not null)
            {
                await userManager.AssignDefaultRoleAsync(account, logger);
            }
        }
    }
}
