using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Controllers;

namespace PersonalFinance.Tests.Security;

/// <summary>
/// Hosts the real ApiService pipeline in-process (InMemory database, shared secret configured) and checks ADR 0004
/// over HTTP: an <c>X-User-Id</c> header only authenticates together with the right <c>X-Internal-Key</c>.
/// </summary>
[Collection("ApiServiceHost")]
public class ApiServiceInternalKeyHttpTests : IClassFixture<ApiServiceInternalKeyHttpTests.ApiServiceHost>
{
    private const string InternalKey = "http-test-shared-secret";

    private static readonly string[] AccountEndpoints =
    [
        "/api/Dashboard/summary",
        "/api/NetWorth/parameters",
        "/api/Watchlist",
        "/api/Notification",
        "/api/Search?q=goal",
        "/api/GoogleDrive/connections",
        "/api/budget-tracker/categories"
    ];

    private readonly HttpClient _client;

    public ApiServiceInternalKeyHttpTests(ApiServiceHost host)
    {
        _client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public static TheoryData<string> Endpoints()
    {
        var data = new TheoryData<string>();
        foreach (var endpoint in AccountEndpoints)
        {
            data.Add(endpoint);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task WithoutAnyHeaders_Returns401(string endpoint)
    {
        var response = await _client.SendAsync(Request(endpoint, userId: null, internalKey: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SpoofedUserIdWithoutKey_Returns401(string endpoint)
    {
        var response = await _client.SendAsync(Request(endpoint, userId: "victim", internalKey: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SpoofedUserIdWithWrongKey_Returns401(string endpoint)
    {
        var response = await _client.SendAsync(Request(endpoint, userId: "victim", internalKey: "guessed"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task UserIdWithTheSharedSecret_IsServed(string endpoint)
    {
        var response = await _client.SendAsync(Request(endpoint, userId: "account-1", internalKey: InternalKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthAndUnknownRoutes_AreNotAffectedByAuthentication()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/NonExistent")).StatusCode);
    }

    private static HttpRequestMessage Request(string endpoint, string? userId, string? internalKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (userId is not null)
        {
            request.Headers.Add("X-User-Id", userId);
        }

        if (internalKey is not null)
        {
            request.Headers.Add("X-Internal-Key", internalKey);
        }

        return request;
    }

    /// <summary>The ApiService entry point (any ApiService type identifies the assembly), InMemory database.</summary>
    public sealed class ApiServiceHost : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"InMemory:ApiHttpTests_{Guid.NewGuid():N}");
            builder.UseSetting("Internal:ApiKey", InternalKey);
        }
    }
}
