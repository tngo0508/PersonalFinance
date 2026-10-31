namespace PersonalFinance.ApiService.Features.InvestmentScenarios;

/// <summary>
/// Dependency-injection registrations of the Investment Scenarios Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007). Its validators are registered with every other Feature's.
/// </summary>
internal static class InvestmentScenariosFeature
{
    /// <summary>Saved Investment Calculator inputs.</summary>
    public static IServiceCollection AddInvestmentScenariosFeature(this IServiceCollection services)
    {
        services.AddScoped<IInvestmentScenarioService, InvestmentScenarioService>();
        return services;
    }
}
