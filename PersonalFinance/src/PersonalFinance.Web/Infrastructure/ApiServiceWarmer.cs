namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Wakes ApiService before a signed-in user needs it. ApiService scales to zero when idle, so without a warm-up
/// the first page after sign-in can fail while its container is still starting.
/// </summary>
public interface IApiServiceWarmer
{
    /// <summary>
    /// Sends an anonymous liveness ping to ApiService, at most once per throttle window.
    /// Never throws, so callers can start it without awaiting it.
    /// </summary>
    Task WarmUpAsync();
}

/// <summary>
/// Pings ApiService's anonymous <c>/alive</c> endpoint. No Account is forwarded: the ping only starts the container.
/// </summary>
public sealed class ApiServiceWarmer : IApiServiceWarmer
{
    /// <summary>Name of the HttpClient registered for the warm-up ping.</summary>
    public const string HttpClientName = "ApiServiceWarmup";

    private const string AlivenessPath = "/alive";
    private static readonly long ThrottleMilliseconds = (long)TimeSpan.FromMinutes(2).TotalMilliseconds;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiServiceWarmer> _logger;
    private long _lastPingAt = long.MinValue;

    public ApiServiceWarmer(IHttpClientFactory httpClientFactory, ILogger<ApiServiceWarmer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task WarmUpAsync()
    {
        long now = Environment.TickCount64;
        long last = Interlocked.Read(ref _lastPingAt);
        if (last != long.MinValue && now - last < ThrottleMilliseconds)
        {
            return Task.CompletedTask;
        }

        // Only the caller that wins the swap pings; concurrent Login page loads share one warm-up.
        if (Interlocked.CompareExchange(ref _lastPingAt, now, last) != last)
        {
            return Task.CompletedTask;
        }

        return PingAsync();
    }

    private async Task PingAsync()
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            using HttpResponseMessage response = await client.GetAsync(AlivenessPath);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ApiService warm-up ping answered {StatusCode}.", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ApiService warm-up ping failed.");
        }
    }
}
