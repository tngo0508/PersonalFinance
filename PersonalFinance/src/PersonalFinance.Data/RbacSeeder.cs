using Microsoft.AspNetCore.Identity;
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
    public static async Task SeedAsync(
        IServiceProvider services,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetService<RoleManager<IdentityRole>>();
        var userManager = services.GetService<UserManager<IdentityUser>>();
        if (roleManager is null || userManager is null)
        {
            logger?.LogInformation("Identity roles are not registered; skipping RBAC seeding.");
            return;
        }

        foreach (var role in AppRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(role))
            {
                var created = await roleManager.CreateAsync(new IdentityRole(role));
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{role}': {string.Join(", ", created.Errors.Select(e => e.Code))}");
                }

                logger?.LogInformation("Created role {Role}.", role);
            }
        }

        var adminEmail = services.GetService<IConfiguration>()?[AdminEmailKey];
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            logger?.LogInformation("{Key} is not configured; no Admin grant performed.", AdminEmailKey);
            return;
        }

        var user = await userManager.FindByEmailAsync(adminEmail.Trim());
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

        var added = await userManager.AddToRoleAsync(user, AppRoles.Admin);
        if (!added.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to grant Admin: {string.Join(", ", added.Errors.Select(e => e.Code))}");
        }

        await userManager.UpdateSecurityStampAsync(user);
        logger?.LogInformation("Granted Admin role to the configured admin account.");
    }
}
