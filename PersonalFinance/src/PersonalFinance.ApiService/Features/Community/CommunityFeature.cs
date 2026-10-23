namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Dependency-injection registrations of the Community Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class CommunityFeature
{
    /// <summary>Community posts and comments.</summary>
    public static IServiceCollection AddCommunityFeature(this IServiceCollection services)
    {
        services.AddScoped<ICommunityService, CommunityService>();
        return services;
    }
}
