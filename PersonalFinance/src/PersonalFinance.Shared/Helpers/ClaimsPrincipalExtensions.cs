using System.Security.Claims;

namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// Reads the signed-in Account from a <see cref="ClaimsPrincipal"/>. Used by Web (cookie sign-in) and
/// ApiService (claims built by <c>XUserIdAuthenticationHandler</c>) so both sides agree on the Account id.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the Account id (<see cref="ClaimTypes.NameIdentifier"/>) of an authenticated principal.
    /// Only call this behind <c>[Authorize]</c>; a missing id is a programming error, not a user error.
    /// </summary>
    /// <exception cref="InvalidOperationException">The principal has no Account id claim.</exception>
    public static string GetUserId(this ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "The current principal has no Account id. Is the endpoint missing [Authorize]?");
        }

        return userId;
    }
}
