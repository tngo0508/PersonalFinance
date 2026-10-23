using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.BudgetTracker;

/// <summary>
/// Dependency-injection registrations of the Budget Tracker Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007). The source-selected month reads it shares with other Features are registered by
/// <c>Infrastructure/InfrastructureServiceExtensions.cs</c> (ADR 0002).
/// </summary>
internal static class BudgetTrackerFeature
{
    /// <summary>The in-app monthly budget: plans, transactions and Actual Overrides.</summary>
    public static IServiceCollection AddBudgetTrackerFeature(this IServiceCollection services)
    {
        services.AddScoped<IMonthlyBudgetService, MonthlyBudgetService>();
        return services;
    }
}
