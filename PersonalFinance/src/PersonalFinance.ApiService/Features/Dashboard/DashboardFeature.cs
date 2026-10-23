using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Features.Dashboard;

/// <summary>
/// Dependency-injection registrations of the Dashboard Feature (GLOSSARY.md), called from <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class DashboardFeature
{
    /// <summary>The signed-in user's overview page.</summary>
    public static IServiceCollection AddDashboardFeature(this IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
