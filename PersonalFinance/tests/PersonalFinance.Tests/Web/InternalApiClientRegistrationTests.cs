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
