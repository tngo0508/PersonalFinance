namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// Dependency-injection registrations of the Savings Goals Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class SavingsGoalsFeature
{
    /// <summary>Savings Goals and their contributions.</summary>
    public static IServiceCollection AddSavingsGoalsFeature(this IServiceCollection services)
    {
        services.AddScoped<ISavingsGoalService, SavingsGoalService>();
        return services;
    }
}
