namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Names of the authorization Policies. Pages and endpoints use these in <c>[Authorize(Policy = ...)]</c>.
/// </summary>
public static class AppPolicies
{
    /// <summary>
    /// Requires an authenticated account holding the <see cref="AppRoles.Admin"/> role.
    /// </summary>
    public const string RequireAdmin = "RequireAdmin";
}
