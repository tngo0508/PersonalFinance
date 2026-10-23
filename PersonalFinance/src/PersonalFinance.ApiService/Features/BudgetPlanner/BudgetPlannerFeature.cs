using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Features.BudgetPlanner;

/// <summary>
/// Dependency-injection registrations of the Budget Planner Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007).
/// </summary>
internal static class BudgetPlannerFeature
{
    /// <summary>The yearly budget allocated across spending categories.</summary>
    public static IServiceCollection AddBudgetPlannerFeature(this IServiceCollection services)
    {
        services.AddScoped<IBudgetService, BudgetService>();
        return services;
    }
}
