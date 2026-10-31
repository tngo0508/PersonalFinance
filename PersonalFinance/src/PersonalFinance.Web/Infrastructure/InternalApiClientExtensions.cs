using System.Net;
using Microsoft.Extensions.Http.Resilience;
using PersonalFinance.Shared.Contracts;
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
    public static IServiceCollection AddInternalApiClients(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<UserIdForwardingHandler>();

        var apiBaseUrl = new Uri(configuration[ApiBaseUrlKey] ?? DefaultApiBaseUrl);

        services.AddInternalApiClient<IAccountDeletionApi>(apiBaseUrl);
        services.AddInternalApiClient<IAdminApi>(apiBaseUrl);
        services.AddInternalApiClient<IAiAssistantApi>(apiBaseUrl, TimeSpan.FromSeconds(30));
        services.AddInternalApiClient<IBudgetTrackerApi>(apiBaseUrl);
        services.AddInternalApiClient<ICommunityApi>(apiBaseUrl);
        services.AddInternalApiClient<ICurrencyConverterApi>(apiBaseUrl);
        services.AddInternalApiClient<IDashboardApi>(apiBaseUrl);
        services.AddInternalApiClient<IDataExportApi>(apiBaseUrl);
        services.AddInternalApiClient<IFeedbackApi>(apiBaseUrl);
        services.AddInternalApiClient<ILearningApi>(apiBaseUrl);
        services.AddInternalApiClient<IMoneyPrioritiesApi>(apiBaseUrl);
        // Spreadsheet downloads from Google Drive can be slow.
        services.AddInternalApiClient<IGoogleDriveApi>(apiBaseUrl, TimeSpan.FromSeconds(35));
        services.AddInternalApiClient<IInvestmentScenariosApi>(apiBaseUrl);
        services.AddInternalApiClient<INetWorthApi>(apiBaseUrl);
        services.AddInternalApiClient<INotificationApi>(apiBaseUrl);
        services.AddInternalApiClient<IBudgetPlannerApi>(apiBaseUrl);
        services.AddInternalApiClient<ISavingsGoalsApi>(apiBaseUrl);
        services.AddInternalApiClient<ISearchApi>(apiBaseUrl);
        services.AddInternalApiClient<ISpreadsheetAnalysisApi>(apiBaseUrl);
        services.AddInternalApiClient<IWatchlistApi>(apiBaseUrl);
        services.AddInternalApiClient<INewsApi>(apiBaseUrl);
        // A Research dataset that has never loaded answers 503 until the next refresh, so retrying it only delays the
        // page (and its Unavailable State) by several seconds per dataset.
        services.AddInternalApiClient<IResearchApi>(apiBaseUrl, retryServiceUnavailable: false);

        // Anonymous liveness ping from the Login page and from Web startup, so ApiService is awake by the time the
        // Dashboard loads.
        services.AddHttpClient(ApiServiceWarmer.HttpClientName, client => client.BaseAddress = apiBaseUrl);
        services.AddSingleton<IApiServiceWarmer, ApiServiceWarmer>();
        services.AddHostedService<ApiServiceStartupWarmer>();

        return services;
    }

    /// <summary>
    /// Registers one Refit contract. With <paramref name="retryServiceUnavailable"/> false, the client keeps the
    /// standard resilience pipeline but neither retries a 503 nor counts it towards the circuit breaker: for that
    /// contract a 503 is an answer ("not available yet"), not a passing fault.
    /// </summary>
    private static void AddInternalApiClient<TContract>(this IServiceCollection services, Uri apiBaseUrl,
        TimeSpan? timeout = null, bool retryServiceUnavailable = true)
        where TContract : class
    {
        IHttpClientBuilder builder = services.AddRefitClient<TContract>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = apiBaseUrl;
                client.Timeout = timeout ?? DefaultTimeout;
            })
            .AddHttpMessageHandler<UserIdForwardingHandler>();
        if (!retryServiceUnavailable)
        {
            // Replaces the standard handler AddServiceDefaults() adds to every client. The API is marked for
            // evaluation; a test pins that this client still makes a single call on a 503.
#pragma warning disable EXTEXP0001
            builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
            builder.AddStandardResilienceHandler(options =>
            {
                options.Retry.ShouldHandle = args => ValueTask.FromResult(IsTransientExceptUnavailable(args.Outcome));
                options.CircuitBreaker.ShouldHandle =
                    args => ValueTask.FromResult(IsTransientExceptUnavailable(args.Outcome));
            });
        }
    }

    /// <summary>The standard transient faults, less a 503 response.</summary>
    private static bool IsTransientExceptUnavailable(Polly.Outcome<HttpResponseMessage> outcome) =>
        HttpClientResiliencePredicates.IsTransient(outcome)
        && outcome.Result?.StatusCode != HttpStatusCode.ServiceUnavailable;
}
