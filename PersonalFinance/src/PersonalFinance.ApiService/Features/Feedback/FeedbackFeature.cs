using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Features.Feedback;

/// <summary>
/// Dependency-injection registrations of the Feedback Feature (GLOSSARY.md), called from <c>Program.cs</c>.
/// Every Feature has one of these (ADR 0007): add a new Service here, not in a shared registration class.
/// </summary>
internal static class FeedbackFeature
{
    /// <summary>Feedback that Accounts send to the operator, with its own submission limit.</summary>
    public static IServiceCollection AddFeedbackFeature(this IServiceCollection services)
    {
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddKeyedSingleton<IRateLimiterService>(FeedbackRateLimit.Key, (_, _) =>
            new RateLimiterService(FeedbackRateLimit.MaxSubmissionsPerWindow, FeedbackRateLimit.Window));
        return services;
    }
}
