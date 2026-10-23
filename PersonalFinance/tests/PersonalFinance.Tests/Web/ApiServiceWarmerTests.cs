using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// The Login page wakes ApiService with an anonymous, throttled <c>/alive</c> ping that never throws.
/// </summary>
public class ApiServiceWarmerTests
{
    [Fact]
    public async Task WarmUp_SendsAnonymousAlivenessPingToApiService()
    {
        var requests = new List<HttpRequestMessage>();
        using ServiceProvider provider = BuildProvider(request =>
        {
            requests.Add(request);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await provider.GetRequiredService<IApiServiceWarmer>().WarmUpAsync();

        HttpRequestMessage request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://apiservice.test/alive", request.RequestUri!.ToString());
        Assert.False(request.Headers.Contains("X-User-Id"));
    }

    [Fact]
    public async Task WarmUp_WithinThrottleWindow_SendsOnePing()
    {
        var requests = new List<HttpRequestMessage>();
        using ServiceProvider provider = BuildProvider(request =>
        {
            requests.Add(request);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        IApiServiceWarmer warmer = provider.GetRequiredService<IApiServiceWarmer>();

        await warmer.WarmUpAsync();
        await warmer.WarmUpAsync();

        Assert.Single(requests);
    }

    [Fact]
    public async Task WarmUp_WhenApiServiceIsUnreachable_DoesNotThrow()
    {
        using ServiceProvider provider = BuildProvider(_ => throw new HttpRequestException("Connection refused"));

        Exception exception = await Record.ExceptionAsync(
            () => provider.GetRequiredService<IApiServiceWarmer>().WarmUpAsync());

        Assert.Null(exception);
    }

    private static ServiceProvider BuildProvider(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiSettings:BaseUrl"] = "http://apiservice.test" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInternalApiClients(configuration);
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder =>
                builder.PrimaryHandler = new DelegatingTestHandler((request, _) => Task.FromResult(respond(request)))));

        return services.BuildServiceProvider();
    }
}
