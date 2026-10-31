using System.Net;
using System.Net.Http.Json;
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
    private const string MacroUrl = "/api/Research/macro";
    private const string FundFactsUrl = "/api/Research/funds/facts";
    private const string FundExposureUrl = "/api/Research/funds/exposure";
    private const string FundIndustriesUrl = "/api/Research/funds/industries";
    private const string WorldUrl = "/api/Research/world";
    private const string StatusUrl = "/api/Research/status";
    private const string InternalKey = "http-test-shared-secret";

    private readonly ApiServiceInternalKeyHttpTests.ApiServiceHost _host;

    public ResearchEndpointTests(ApiServiceInternalKeyHttpTests.ApiServiceHost host)
    {
        _host = host;
    }

    private sealed class FakeResearchService(SectorsSnapshotDto? sectors, WorldSnapshotDto? world = null,
        FundsSnapshotDto? funds = null, FundOverlapSnapshotDto? overlap = null,
        FundReturnsSnapshotDto? returns = null, MacroSnapshotDto? macro = null, FundFactsSnapshotDto? facts = null,
        FundExposureSnapshotDto? exposure = null, FundIndustriesSnapshotDto? industries = null) : IResearchService
    {
        public Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<SectorsSnapshotDto>(sectors));

        public Task<ResearchSnapshotResult<FundsSnapshotDto>> GetFundsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundsSnapshotDto>(funds));

        public Task<ResearchSnapshotResult<FundOverlapSnapshotDto>> GetFundOverlapAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundOverlapSnapshotDto>(overlap));

        public Task<ResearchSnapshotResult<FundReturnsSnapshotDto>> GetFundReturnsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundReturnsSnapshotDto>(returns));

        public Task<ResearchSnapshotResult<MacroSnapshotDto>> GetMacroAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<MacroSnapshotDto>(macro));

        public Task<ResearchSnapshotResult<FundFactsSnapshotDto>> GetFundFactsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundFactsSnapshotDto>(facts));

        public Task<ResearchSnapshotResult<FundExposureSnapshotDto>> GetFundExposureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundExposureSnapshotDto>(exposure));

        public Task<ResearchSnapshotResult<FundIndustriesSnapshotDto>> GetFundIndustriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<FundIndustriesSnapshotDto>(industries));

        public Task<ResearchSnapshotResult<WorldSnapshotDto>> GetWorldAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchSnapshotResult<WorldSnapshotDto>(world));

        public Task<ResearchStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchStatusDto());

        public Task<ResearchRefreshSummary> RefreshAsync(ResearchSources sources = ResearchSources.All,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The endpoint never refreshes.");
    }

    // Never loaded is proved once, over HTTP, through the real controller, service and store: see the theory below.
    // These facts prove each action returns its own dataset; Assert.Same fails if an action reads another one.

    [Fact]
    public async Task GetSectors_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new SectorsSnapshotDto { AsOf = new DateOnly(2025, 6, 30), Periods = ["2025"] };

        IActionResult result = await new ResearchController(new FakeResearchService(snapshot)).GetSectors();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFunds_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundsSnapshotDto { AsOf = new DateOnly(2026, 6, 30), Periods = ["2026 Q2"] };

        IActionResult result = await new ResearchController(new FakeResearchService(null, funds: snapshot)).GetFunds();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFundOverlap_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundOverlapSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, overlap: snapshot)).GetFundOverlap();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFundReturns_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundReturnsSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, returns: snapshot)).GetFundReturns();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetMacro_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new MacroSnapshotDto { AsOf = new DateOnly(2026, 9, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, macro: snapshot)).GetMacro();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFundFacts_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundFactsSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, facts: snapshot)).GetFundFacts();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFundExposure_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundExposureSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, exposure: snapshot)).GetFundExposure();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetFundIndustries_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new FundIndustriesSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };

        IActionResult result = await new ResearchController(new FakeResearchService(null, industries: snapshot)).GetFundIndustries();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetWorld_WhenLoaded_ReturnsTheSnapshot()
    {
        var snapshot = new WorldSnapshotDto { AsOf = new DateOnly(2026, 10, 8), Periods = ["2024"] };

        IActionResult result = await new ResearchController(new FakeResearchService(null, snapshot)).GetWorld();

        Assert.Same(snapshot, Assert.IsType<OkObjectResult>(result).Value);
    }

    // A 401 here also proves the controller requires sign-in: ApiService has no fallback policy, so an
    // [AllowAnonymous] action would answer without the shared secret.
    [Theory]
    [InlineData(SectorsUrl)]
    [InlineData(FundsUrl)]
    [InlineData(FundOverlapUrl)]
    [InlineData(FundReturnsUrl)]
    [InlineData(MacroUrl)]
    [InlineData(FundFactsUrl)]
    [InlineData(FundExposureUrl)]
    [InlineData(FundIndustriesUrl)]
    [InlineData(WorldUrl)]
    public async Task Get_OverHttp_WithoutTheSharedSecret_IsRejected(string url)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-User-Id", "account-1");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(SectorsUrl, ResearchController.SectorsNeverLoaded)]
    [InlineData(FundsUrl, ResearchController.FundsNeverLoaded)]
    [InlineData(FundOverlapUrl, ResearchController.FundOverlapNeverLoaded)]
    [InlineData(FundReturnsUrl, ResearchController.FundReturnsNeverLoaded)]
    [InlineData(MacroUrl, ResearchController.MacroNeverLoaded)]
    [InlineData(FundFactsUrl, ResearchController.FundFactsNeverLoaded)]
    [InlineData(FundExposureUrl, ResearchController.FundExposureNeverLoaded)]
    [InlineData(FundIndustriesUrl, ResearchController.FundIndustriesNeverLoaded)]
    [InlineData(WorldUrl, ResearchController.WorldNeverLoaded)]
    public async Task Get_OverHttp_SignedIn_OnAnEmptyDatabase_Returns503WithItsReason(string url, string reason)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        ResearchUnavailableDto? body = await response.Content.ReadFromJsonAsync<ResearchUnavailableDto>();
        Assert.Equal(reason, body?.Reason);
    }

    [Fact]
    public async Task Status_RequiresSignIn_AndReturns200()
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, StatusUrl);
        anonymous.Headers.Add("X-User-Id", "account-1");
        using var signedIn = new HttpRequestMessage(HttpMethod.Get, StatusUrl);
        signedIn.Headers.Add("X-User-Id", "account-1");
        signedIn.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage rejected = await client.SendAsync(anonymous);
        HttpResponseMessage accepted = await client.SendAsync(signedIn);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        ResearchStatusDto? body = await accepted.Content.ReadFromJsonAsync<ResearchStatusDto>();
        Assert.NotNull(body);
        Assert.Empty(body.Datasets);
    }
}
