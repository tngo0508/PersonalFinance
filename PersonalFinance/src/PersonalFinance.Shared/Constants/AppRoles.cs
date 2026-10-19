namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Names of the application Roles. Code must authorize against <see cref="AppPolicies"/>, not against these names directly.
/// </summary>
public static class AppRoles
{
    /// <summary>
    /// The operator of the application. Can reach admin pages and admin API endpoints.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Every Role the database seeder must ensure exists.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Admin];
}
