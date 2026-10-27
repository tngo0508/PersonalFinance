using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Dependency-injection registrations for what two or more Features share (ADR 0007): the clock, the
/// source-selected budget reads of ADR 0002 that the Budget Tracker, Analysis, Dashboard, Search and the AI Assistant
/// all go through, the currency converter of ADR 0016, the per-user rate limiter, and Notifications. Called from
/// <c>Program.cs</c> before any Feature.
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddApiServiceInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // The currency converter (ADR 0016): the only place amounts are brought into the Reporting Currency, over an
        // in-memory rate cache the daily rate refresh evicts.
        services.AddMemoryCache();
        services.AddSingleton<ExchangeRateCache>();
        services.AddScoped<ICurrencyConverter, CurrencyConverter>();

        // The Data Source provider: the one place that decides between synced sheet data and Budget Tracker data.
        services.AddScoped<IBudgetDataSourceService, BudgetDataSourceService>();
        services.AddScoped<SheetBudgetMonthReader>();
        services.AddScoped<TrackerBudgetMonthReader>();
        services.AddScoped<IBudgetDataProvider, BudgetDataProvider>();

        // The AI Assistant's per-user limiter; Feedback registers its own keyed instance with FeedbackRateLimit's limits.
        services.AddSingleton<IRateLimiterService, RateLimiterService>();

        // Notifications: raised by the Budget Planner, Analysis and Savings Goals, read through NotificationController.
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
