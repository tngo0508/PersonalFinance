using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Features.Admin;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// Calls the real ApiService pipeline over HTTP, as the Web app does, so a rejection a service raises as an
/// <c>ApiErrorException</c> is proven to reach the wire as an <c>ApiErrorDto</c> with its status code (ADR 0017).
/// This guards the global <c>ApiErrorExceptionFilter</c> registration in <c>Program.cs</c>, which no unit test sees:
/// without it the exception would surface as a 500.
/// </summary>
[Collection("ApiServiceHost")]
public class ApiErrorWireFormatTests : IClassFixture<ApiErrorWireFormatTests.ApiServiceHost>
{
    private const string InternalKey = "api-error-wire-format-secret";

    private readonly ApiServiceHost _host;

    public ApiErrorWireFormatTests(ApiServiceHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task ServiceRejection_ArrivesAsAnApiErrorDto_WithItsStatusCode()
    {
        using HttpClient client = ClientFor($"wire-format-{Guid.NewGuid():N}");

        using HttpResponseMessage response =
            await client.GetAsync(new Uri("/api/community/poll-comments?pollCode=no-such-poll", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement errors = body.RootElement.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        JsonElement entry = Assert.Single(errors.EnumerateArray());

        Assert.Equal(ApiErrorKeys.Community.PollNotFound, entry.GetProperty("key").GetString());
        Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("message").GetString()));
        Assert.False(entry.TryGetProperty("field", out _), "a whole-request error carries no field");
        Assert.False(entry.TryGetProperty("args", out _), "a message without placeholders carries no args");
        Assert.False(entry.TryGetProperty("Key", out _), "the contract is camelCase");
    }

    private HttpClient ClientFor(string userId)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Internal-Key", InternalKey);
        return client;
    }

    /// <summary>The ApiService entry point, InMemory database, shared secret configured.</summary>
    public sealed class ApiServiceHost : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                $"InMemory:ApiErrorWireFormat_{Guid.NewGuid():N}");
            builder.UseSetting("Internal:ApiKey", InternalKey);
        }
    }
}
