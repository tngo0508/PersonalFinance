namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Wakes ApiService when a Web replica starts. Both apps scale to zero, so a visitor who wakes the Web usually needs
/// ApiService next; pinging it now lets both containers start in parallel instead of one after the other.
/// </summary>
public sealed class ApiServiceStartupWarmer : IHostedService
{
    private readonly IApiServiceWarmer _apiServiceWarmer;

    public ApiServiceStartupWarmer(IApiServiceWarmer apiServiceWarmer)
    {
        _apiServiceWarmer = apiServiceWarmer;
    }

    /// <summary>Starts the ping without awaiting it, so Web startup never waits for ApiService.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _apiServiceWarmer.WarmUpAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
