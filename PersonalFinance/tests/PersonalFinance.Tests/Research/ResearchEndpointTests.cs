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
/// The ApiService <c>GET /api/Research/sectors</c>, <c>/funds</c> and <c>/world</c> endpoints: signed-in only; each
/// returns its stored snapshot, or 503 with a reason when the dataset has never loaded.
/// </summary>
[Collection("ApiServiceHost")]
public class ResearchEndpointTests : IClassFixture<ApiServiceInternalKeyHttpTests.ApiServiceHost>
{
    private const string SectorsUrl = "/api/Research/sectors";
    private const string FundsUrl = "/api/Research/funds";
    private const string FundOverlapUrl = "/api/Research/funds/overlap";
    private const string FundReturnsUrl = "/api/Research/funds/returns";
    private const string WorldUrl = "/api/Research/world";
    private const string InternalKey = "http-test-shared-secret";

    private readonly ApiServiceInternalKeyHttpTests.ApiServiceHost _host;

    public ResearchEndpointTests(ApiServiceInternalKeyHttpTests.ApiServiceHost host)
    {
        _host = host;
    }

    private sealed class FakeResearchService(SectorsSnapshotDto? sectors, WorldSnapshotDto? world = null,
        FundsSnapshotDto? funds = null, FundOverlapSnapshotDto? overlap = null,
        FundReturnsSnapshotDto? returns = null) : IResearchService
    {
        public Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<SectorsSnapshotDto>(sectors));

        public Task<ResearchSnapshotResult<FundsSnapshotDto>> GetFundsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundsSnapshotDto>(funds));

        public Task<ResearchSnapshotResult<FundOverlapSnapshotDto>> GetFundOverlapAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundOverlapSnapshotDto>(overlap));

        public Task<ResearchSnapshotResult<FundReturnsSnapshotDto>> GetFundReturnsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundReturnsSnapshotDto>(returns));

        public Task<ResearchSnapshotResult<WorldSnapshotDto>> GetWorldAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<WorldSnapshotDto>(world));

        public Task<ResearchRefreshSummary> RefreshAsync(ResearchSources sources = ResearchSources.All,
            CancellationToken cancellationToken = default) =>
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
    public async Task GetFunds_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundsSnapshotDto { AsOf = new DateOnly(2026, 6, 30), Periods = ["2026 Q2"] };

        IActionResult result = await new ResearchController(new FakeResearchService(null, funds: snapshot)).GetFunds();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFunds_WhenNeverLoaded_Returns503WithAReason()
    {
        IActionResult result = await new ResearchController(new FakeResearchService(new SectorsSnapshotDto())).GetFunds();

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.Equal(ResearchController.FundsNeverLoaded, Assert.IsType<ResearchUnavailableDto>(objectResult.Value).Reason);
    }

    [Fact]
    public async Task GetFundOverlap_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundOverlapSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, overlap: snapshot)).GetFundOverlap();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFundOverlap_WhenNeverLoaded_Returns503WithAReason()
    {
        IActionResult result = await new ResearchController(new FakeResearchService(null, funds: new FundsSnapshotDto()))
            .GetFundOverlap();

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.Equal(ResearchController.FundOverlapNeverLoaded,
            Assert.IsType<ResearchUnavailableDto>(objectResult.Value).Reason);
    }

    [Fact]
    public async Task GetFundReturns_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundReturnsSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, returns: snapshot)).GetFundReturns();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFundReturns_WhenNeverLoaded_Returns503WithAReason()
    {
        IActionResult result = await new ResearchController(new FakeResearchService(null, funds: new FundsSnapshotDto()))
            .GetFundReturns();

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.Equal(ResearchController.FundReturnsNeverLoaded,
            Assert.IsType<ResearchUnavailableDto>(objectResult.Value).Reason);
    }

    [Theory]
    [InlineData(SectorsUrl)]
    [InlineData(FundsUrl)]
    [InlineData(FundOverlapUrl)]
    [InlineData(FundReturnsUrl)]
    public async Task Get_OverHttp_WithoutTheSharedSecret_IsRejected(string url)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-User-Id", "account-1");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(SectorsUrl)]
    [InlineData(FundsUrl)]
    [InlineData(FundOverlapUrl)]
    [InlineData(FundReturnsUrl)]
    public async Task Get_OverHttp_SignedIn_OnAnEmptyDatabase_Returns503WithAReason(string url)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        ResearchUnavailableDto? body = await response.Content.ReadFromJsonAsync<ResearchUnavailableDto>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Reason));
    }

    [Fact]
    public async Task GetWorld_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new WorldSnapshotDto { AsOf = new DateOnly(2026, 10, 8), Periods = ["2024"] };

        IActionResult result = await new ResearchController(new FakeResearchService(null, snapshot)).GetWorld();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetWorld_WhenNeverLoaded_Returns503WithAReason()
    {
        IActionResult result = await new ResearchController(new FakeResearchService(null)).GetWorld();

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.Equal(ResearchController.WorldNeverLoaded, Assert.IsType<ResearchUnavailableDto>(objectResult.Value).Reason);
    }

    [Fact]
    public async Task GetWorld_OverHttp_WithoutTheSharedSecret_IsRejected()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, WorldUrl);
        request.Headers.Add("X-User-Id", "account-1");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorld_OverHttp_SignedIn_OnAnEmptyDatabase_Returns503WithAReason()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, WorldUrl);
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        ResearchUnavailableDto? body = await response.Content.ReadFromJsonAsync<ResearchUnavailableDto>();
        Assert.Equal(ResearchController.WorldNeverLoaded, body?.Reason);
    }
}
