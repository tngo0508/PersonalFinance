using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;
using PersonalFinance.ApiService.Services;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Dependency-injection registrations for ApiService, one method per feature (see <c>GLOSSARY.md</c>).
/// A new Service is registered in its feature's method; a new feature gets its own method, called from <c>Program.cs</c>.
/// </summary>
public static class FeatureServiceExtensions
{
    private static readonly TimeSpan GoogleDriveTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Budget Planner, Budget Tracker, and the source-selected budget reads (ADR 0002).</summary>
    public static IServiceCollection AddBudgetFeature(this IServiceCollection services)
    {
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IMonthlyBudgetService, MonthlyBudgetService>();
        services.AddScoped<IBudgetDataSourceService, BudgetDataSourceService>();
        services.AddScoped<SheetBudgetMonthReader>();
        services.AddScoped<TrackerBudgetMonthReader>();
        services.AddScoped<IBudgetDataProvider, BudgetDataProvider>();
        return services;
    }

    /// <summary>Google Drive connections, spreadsheet reading, and Spreadsheet Analysis.</summary>
    public static IServiceCollection AddGoogleDriveFeature(this IServiceCollection services)
    {
        services.AddHttpClient<IDriveConnectionService, DriveConnectionService>(client => client.Timeout = GoogleDriveTimeout);
        services.AddHttpClient<IGoogleDriveFileSource, GoogleDriveFileSource>(client => client.Timeout = GoogleDriveTimeout);
        services.AddScoped<ISpreadsheetReader, SpreadsheetReaderService>();
        services.AddScoped<ISpreadsheetAnalysisService, SpreadsheetAnalysisService>();
        return services;
    }

    /// <summary>Net Worth parameters and projections.</summary>
    public static IServiceCollection AddNetWorthFeature(this IServiceCollection services)
    {
        services.AddScoped<INetWorthParametersService, NetWorthParametersService>();
        return services;
    }

    /// <summary>Dashboard, Savings Goals, Watchlist, Community, Notifications and Search.</summary>
    public static IServiceCollection AddDashboardFeatures(this IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISavingsGoalService, SavingsGoalService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ISearchService, SearchService>();
        return services;
    }

    /// <summary>Feedback that Accounts send to the operator.</summary>
    public static IServiceCollection AddFeedbackFeature(this IServiceCollection services)
    {
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddKeyedSingleton<IRateLimiterService>(FeedbackRateLimit.Key, (_, _) =>
            new RateLimiterService(FeedbackRateLimit.MaxSubmissionsPerWindow, FeedbackRateLimit.Window));
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

        var openAiApiKey = configuration.GetValue<string>("OpenAI:ApiKey");
        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            var openAiModel = configuration.GetValue<string>("OpenAI:Model") ?? "gpt-4o-mini";
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
