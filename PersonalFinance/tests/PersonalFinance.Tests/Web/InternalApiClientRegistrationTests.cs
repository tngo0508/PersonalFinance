using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Infrastructure;
using Refit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// Every Refit contract Web uses to call ApiService must be registered through one helper that forwards the Account.
/// </summary>
public class InternalApiClientRegistrationTests
{
    [Fact]
    public void EveryApiServiceContract_IsRegistered()
    {
        using ServiceProvider provider = BuildProvider(out _);

        var contracts = typeof(IGoogleDriveApi).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace == typeof(IGoogleDriveApi).Namespace)
            .Where(t => t.GetMethods().Any(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Length > 0))
            .ToList();

        Assert.NotEmpty(contracts);
        var missing = contracts.Where(t => provider.GetService(t) is null).Select(t => t.Name).ToList();
        Assert.True(missing.Count == 0, $"Refit contracts not registered in Web: {string.Join(", ", missing)}");
    }

    [Fact]
    public async Task RegisteredClient_ForwardsSignedInAccountAndInternalKey()
    {
        using ServiceProvider provider = BuildProvider(out List<HttpRequestMessage>? sentRequests);
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "account-1") }, "Cookies"))
        };

        await provider.GetRequiredService<IGoogleDriveApi>().GetUserConnectionsAsync();

        HttpRequestMessage request = Assert.Single(sentRequests);
        Assert.Equal("http://apiservice.test/api/googledrive/connections", request.RequestUri!.ToString());
        Assert.Equal("account-1", request.Headers.GetValues("X-User-Id").Single());
        Assert.Equal("shared-secret", request.Headers.GetValues("X-Internal-Key").Single());
    }

    [Fact]
    public async Task ResearchClient_DoesNotRetryAServiceUnavailableAnswer()
    {
        // A Research dataset that has never loaded answers 503 until the next refresh: retrying it only delays the page.
        using ServiceProvider provider = BuildResilientProvider(_ => HttpStatusCode.ServiceUnavailable, out List<HttpRequestMessage> sent);

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => provider.GetRequiredService<IResearchApi>().GetSectorsAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        Assert.Single(sent);
    }

    [Fact]
    public async Task ResearchClient_StillRetriesOtherTransientFaults()
    {
        using ServiceProvider provider = BuildResilientProvider(
            call => call == 1 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK, out List<HttpRequestMessage> sent);

        await provider.GetRequiredService<IResearchApi>().GetSectorsAsync();

        Assert.Equal(2, sent.Count);
    }

    [Fact]
    public async Task OtherClients_StillRetryAServiceUnavailableAnswer()
    {
        using ServiceProvider provider = BuildResilientProvider(
            call => call == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK, out List<HttpRequestMessage> sent);

        await provider.GetRequiredService<IGoogleDriveApi>().GetUserConnectionsAsync();

        Assert.Equal(2, sent.Count);
    }

    /// <summary>
    /// The clients with the standard resilience handler on every HttpClient, as <c>AddServiceDefaults()</c> adds it,
    /// over a handler answering each call with <paramref name="statusOf"/> its 1-based number.
    /// </summary>
    private static ServiceProvider BuildResilientProvider(Func<int, HttpStatusCode> statusOf,
        out List<HttpRequestMessage> sentRequests)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiSettings:BaseUrl"] = "http://apiservice.test" })
            .Build();
        var requests = new List<HttpRequestMessage>();
        sentRequests = requests;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());
        services.AddInternalApiClients(configuration);
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder =>
                builder.PrimaryHandler = new DelegatingTestHandler((request, _) =>
                {
                    lock (requests)
                    {
                        requests.Add(request);
                        HttpStatusCode status = statusOf(requests.Count);
                        return Task.FromResult(new HttpResponseMessage(status)
                        {
                            RequestMessage = request,
                            // A Research snapshot is an object; the Google Drive connections are a list.
                            Content = new StringContent(
                                status == HttpStatusCode.OK && !request.RequestUri!.AbsolutePath.Contains("Research", StringComparison.Ordinal)
                                    ? "[]"
                                    : "{}",
                                Encoding.UTF8, "application/json")
                        });
                    }
                })));

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildProvider(out List<HttpRequestMessage> sentRequests)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiSettings:BaseUrl"] = "http://apiservice.test",
                ["Internal:ApiKey"] = "shared-secret"
            })
            .Build();

        var requests = new List<HttpRequestMessage>();
        sentRequests = requests;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInternalApiClients(configuration);
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder =>
                builder.PrimaryHandler = new DelegatingTestHandler((request, _) =>
                {
                    requests.Add(request);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", Encoding.UTF8, "application/json")
                    });
                })));

        return services.BuildServiceProvider();
    }
}
