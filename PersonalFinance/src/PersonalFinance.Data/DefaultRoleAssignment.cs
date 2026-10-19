using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Data;

/// <summary>
/// Assigns the default <see cref="AppRoles.User"/> role to newly created accounts.
/// </summary>
public static class DefaultRoleAssignment
{
    /// <summary>
    /// Adds the account to the User role. Never throws: a failure is logged and an Admin can assign the role later.
    /// </summary>
    public static async Task AssignDefaultRoleAsync(
        this UserManager<IdentityUser> userManager,
        IdentityUser user,
        ILogger? logger = null)
    {
        try
        {
            var result = await userManager.AddToRoleAsync(user, AppRoles.User);
            if (!result.Succeeded)
            {
                logger?.LogWarning(
                    "Could not assign default role {Role}: {Errors}",
                    AppRoles.User,
                    string.Join(", ", result.Errors.Select(e => e.Code)));
            }
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogWarning(ex, "Default role {Role} is not available; the account was created without it.", AppRoles.User);
        }
    }
}
