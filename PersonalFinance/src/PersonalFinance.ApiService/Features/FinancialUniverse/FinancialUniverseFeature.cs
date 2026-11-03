namespace PersonalFinance.ApiService.Features.FinancialUniverse;

/// <summary>
/// Dependency-injection registrations of the Financial Universe Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007).
/// </summary>
internal static class FinancialUniverseFeature
{
    /// <summary>The Financial Universe graph projection.</summary>
    public static IServiceCollection AddFinancialUniverseFeature(this IServiceCollection services)
    {
        services.AddScoped<IFinancialUniverseService, FinancialUniverseService>();
        return services;
    }
}
