using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Services;
using Refit;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Registers the Refit clients Web uses to call ApiService. Every client forwards the signed-in Account and the
/// shared secret through <see cref="UserIdForwardingHandler"/>; resilience (retry, timeouts, circuit breaker) and
/// service discovery come from <c>AddServiceDefaults()</c>, which applies them to every HttpClient.
/// </summary>
public static class InternalApiClientExtensions
{
    private const string ApiBaseUrlKey = "ApiSettings:BaseUrl";
    private const string DefaultApiBaseUrl = "https+http://apiservice";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Registers every ApiService Refit contract. Add new contracts here, not in <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddInternalApiClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<UserIdForwardingHandler>();

        var apiBaseUrl = new Uri(configuration[ApiBaseUrlKey] ?? DefaultApiBaseUrl);

        services.AddInternalApiClient<IAdminApi>(apiBaseUrl);
        services.AddInternalApiClient<IAiAssistantApi>(apiBaseUrl, TimeSpan.FromSeconds(30));
        services.AddInternalApiClient<IBudgetTrackerApi>(apiBaseUrl);
        services.AddInternalApiClient<ICommunityApi>(apiBaseUrl);
        services.AddInternalApiClient<IDashboardApi>(apiBaseUrl);
        services.AddInternalApiClient<IFeedbackApi>(apiBaseUrl);
        // Spreadsheet downloads from Google Drive can be slow.
        services.AddInternalApiClient<IGoogleDriveApi>(apiBaseUrl, TimeSpan.FromSeconds(35));
        services.AddInternalApiClient<INetWorthApi>(apiBaseUrl);
        services.AddInternalApiClient<INotificationApi>(apiBaseUrl);
        services.AddInternalApiClient<IBudgetPlannerApi>(apiBaseUrl);
        services.AddInternalApiClient<ISavingsGoalsApi>(apiBaseUrl);
        services.AddInternalApiClient<ISearchApi>(apiBaseUrl);
        services.AddInternalApiClient<ISpreadsheetAnalysisApi>(apiBaseUrl);
        services.AddInternalApiClient<ITodoApi>(apiBaseUrl);
        services.AddInternalApiClient<IWatchlistApi>(apiBaseUrl);

        // Anonymous liveness ping from the Login page, so ApiService is awake by the time the Dashboard loads.
        services.AddHttpClient(ApiServiceWarmer.HttpClientName, client => client.BaseAddress = apiBaseUrl);
        services.AddSingleton<IApiServiceWarmer, ApiServiceWarmer>();

        return services;
    }

    private static void AddInternalApiClient<TContract>(this IServiceCollection services, Uri apiBaseUrl,
        TimeSpan? timeout = null)
        where TContract : class
    {
        services.AddRefitClient<TContract>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = apiBaseUrl;
                client.Timeout = timeout ?? DefaultTimeout;
            })
            .AddHttpMessageHandler<UserIdForwardingHandler>();
    }
}
