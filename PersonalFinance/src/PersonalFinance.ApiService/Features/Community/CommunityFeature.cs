using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Dependency-injection registrations of the Community Feature (the Guild Hall, GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class CommunityFeature
{
    /// <summary>Pseudonyms, Guild Hall posts and comments.</summary>
    public static IServiceCollection AddCommunityFeature(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICommunityService, CommunityService>();
        return services;
    }
}
