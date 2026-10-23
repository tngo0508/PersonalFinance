namespace PersonalFinance.ApiService.Features.Admin;

/// <summary>
/// Dependency-injection registrations of the Admin Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class AdminFeature
{
    /// <summary>Admin-only system information and role management.</summary>
    public static IServiceCollection AddAdminFeature(this IServiceCollection services)
    {
        services.AddScoped<IAdminService, AdminService>();
        return services;
    }
}
