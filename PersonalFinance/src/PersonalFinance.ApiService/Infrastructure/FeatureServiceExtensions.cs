using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Dependency-injection registrations for the Features not yet moved to <c>Features/&lt;Feature&gt;/</c> (ADR 0007),
/// one method per feature. A moved Feature registers itself in its own <c>&lt;Feature&gt;Feature</c> class, as
/// <c>Features/Feedback/FeedbackFeature.cs</c> does; this class empties as the move completes.
/// </summary>
public static class FeatureServiceExtensions
{
    /// <summary>Savings Goals, Watchlist, Community and Notifications, until #159 moves them.</summary>
    public static IServiceCollection AddRemainingFeatures(this IServiceCollection services)
    {
        services.AddScoped<ISavingsGoalService, SavingsGoalService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }

    /// <summary>The Data Export, the Deletion Request and the Purge (ADR 0005).</summary>
    public static IServiceCollection AddPersonalDataFeature(this IServiceCollection services)
    {
        services.AddScoped<IDataExportService, DataExportService>();
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        services.AddScoped<IAccountPurgeService, AccountPurgeService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>Admin-only system information.</summary>
    public static IServiceCollection AddAdminFeature(this IServiceCollection services)
    {
        services.AddScoped<IAdminService, AdminService>();
        return services;
    }

    /// <summary>
    /// AI Assistant: scope filter, spend quota, per-user rate limiting and, when <c>OpenAI:ApiKey</c> is configured,
    /// the OpenAI chat client with automatic tool calling. Without a key the assistant answers with its
    /// "unavailable" fallback.
    /// </summary>
    public static IServiceCollection AddAiAssistantFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        services.AddSingleton<IAiScopeFilter, AiScopeFilter>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddSingleton<IRateLimiterService, RateLimiterService>();

        string? openAiApiKey = configuration.GetValue<string>("OpenAI:ApiKey");
        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            string openAiModel = configuration.GetValue<string>("OpenAI:Model") ?? "gpt-4o-mini";
            services.AddSingleton<IChatClient>(sp =>
                new OpenAIClient(openAiApiKey)
                    .GetChatClient(openAiModel)
                    .AsIChatClient()
                    .AsBuilder()
                    .UseFunctionInvocation(sp.GetService<ILoggerFactory>(),
                        client => client.MaximumIterationsPerRequest = 3)
                    .Build());
        }

        return services;
    }
}
