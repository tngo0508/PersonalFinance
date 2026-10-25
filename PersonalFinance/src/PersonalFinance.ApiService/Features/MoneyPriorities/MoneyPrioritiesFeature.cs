namespace PersonalFinance.ApiService.Features.MoneyPriorities;

/// <summary>
/// Dependency-injection registrations of the Money Priorities Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007). Its validators are registered by the assembly scan.
/// </summary>
internal static class MoneyPrioritiesFeature
{
    /// <summary>Priority Walks: evaluating, saving, listing and deleting them.</summary>
    public static IServiceCollection AddMoneyPrioritiesFeature(this IServiceCollection services)
    {
        services.AddScoped<IMoneyPrioritiesService, MoneyPrioritiesService>();
        return services;
    }
}
