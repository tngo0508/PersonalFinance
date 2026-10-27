using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Features.Admin;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The anonymous endpoints the real ApiService <c>Program.cs</c> maps, called over HTTP. This used to rebuild the
/// mapping inside the test and assert the routes it had just added, so it could not fail when <c>Program.cs</c>
/// changed, and it never saw the <c>ApiDocs:Enabled</c> switch the Bicep templates set (#350). /health and /alive
/// belong to <see cref="ServiceDefaultsTests"/>.
/// </summary>
[Collection("ApiServiceHost")]
public class ApiServiceEndpointTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.NotFound)]
    [InlineData("false", HttpStatusCode.NotFound)]
    [InlineData("true", HttpStatusCode.OK)]
    public async Task ApiDocs_OutsideDevelopment_AreServedOnlyWhenApiDocsIsEnabled(string? apiDocsEnabled,
        HttpStatusCode expected)
    {
        await using var host = new ApiServiceHost(apiDocsEnabled);
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage openApi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        using HttpResponseMessage scalar = await client.GetAsync(new Uri("/scalar/v1", UriKind.Relative));

        Assert.Equal(expected, openApi.StatusCode);
        Assert.Equal(expected, scalar.StatusCode);
    }

    [Fact]
    public async Task VersionBanner_AtTheRoot_NamesTheApplicationAndItsVersion()
    {
        await using var host = new ApiServiceHost(apiDocsEnabled: null);
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(AppVersion.ApplicationName, body.RootElement.GetProperty("application").GetString());
        Assert.Equal(AppVersion.Current, body.RootElement.GetProperty("version").GetString());
        Assert.Equal("Online", body.RootElement.GetProperty("status").GetString());
    }

    /// <summary>
    /// The ApiService entry point outside Development, on an InMemory database, with <c>ApiDocs:Enabled</c> set to
    /// <paramref name="apiDocsEnabled"/> (left unset when null).
    /// </summary>
    private sealed class ApiServiceHost(string? apiDocsEnabled) : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"InMemory:ApiServiceEndpoints_{Guid.NewGuid():N}");
            if (apiDocsEnabled is not null)
            {
                builder.UseSetting("ApiDocs:Enabled", apiDocsEnabled);
            }
        }
    }
}
