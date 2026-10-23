using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Features.Search;

/// <summary>
/// Dependency-injection registrations of the Global Search Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007).
/// </summary>
internal static class SearchFeature
{
    /// <summary>Global Search over the Account's own records.</summary>
    public static IServiceCollection AddSearchFeature(this IServiceCollection services)
    {
        services.AddScoped<ISearchService, SearchService>();
        return services;
    }
}
