using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.Security;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The ApiService <c>GET /api/Research/sectors</c> endpoint: signed-in only; it returns the stored snapshot, or 503
/// with a reason when the dataset has never loaded.
/// </summary>
[Collection("ApiServiceHost")]
public class ResearchEndpointTests : IClassFixture<ApiServiceInternalKeyHttpTests.ApiServiceHost>
{
    private const string SectorsUrl = "/api/Research/sectors";
    private const string InternalKey = "http-test-shared-secret";

    private readonly ApiServiceInternalKeyHttpTests.ApiServiceHost _host;

    public ResearchEndpointTests(ApiServiceInternalKeyHttpTests.ApiServiceHost host)
    {
        _host = host;
    }

    private sealed class FakeResearchService(SectorsSnapshotDto? sectors) : IResearchService
    {
        public Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(sectors is null
                ? ResearchSnapshotResult<SectorsSnapshotDto>.NotLoaded()
                : ResearchSnapshotResult<SectorsSnapshotDto>.Loaded(sectors));

        public Task<ResearchRefreshSummary> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The endpoint never refreshes.");
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        Assert.NotNull(typeof(ResearchController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task GetSectors_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new SectorsSnapshotDto { AsOf = new DateOnly(2025, 6, 30), Periods = ["2025"] };

        IActionResult result = await new ResearchController(new FakeResearchService(snapshot)).GetSectors();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetSectors_WhenNeverLoaded_Returns503WithAReason()
    {
        IActionResult result = await new ResearchController(new FakeResearchService(null)).GetSectors();

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<ResearchUnavailableDto>(objectResult.Value).Reason));
    }

    [Fact]
    public async Task GetSectors_OverHttp_WithoutTheSharedSecret_IsRejected()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, SectorsUrl);
        request.Headers.Add("X-User-Id", "account-1");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSectors_OverHttp_SignedIn_OnAnEmptyDatabase_Returns503WithAReason()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, SectorsUrl);
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        ResearchUnavailableDto? body = await response.Content.ReadFromJsonAsync<ResearchUnavailableDto>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Reason));
    }
}
