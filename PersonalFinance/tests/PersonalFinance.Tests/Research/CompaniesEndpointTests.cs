using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.Security;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The ApiService <c>GET /api/Research/companies/suggest</c>, <c>/api/Research/companies/{ticker}</c> and
/// <c>/api/Research/companies/{ticker}/releases</c> endpoints:
/// signed-in only; input is checked before the service is called; each outcome maps to its status code.
/// </summary>
[Collection("ApiServiceHost")]
public class CompaniesEndpointTests : IClassFixture<ApiServiceInternalKeyHttpTests.ApiServiceHost>
{
    private const string InternalKey = "http-test-shared-secret";

    private readonly ApiServiceInternalKeyHttpTests.ApiServiceHost _host;

    public CompaniesEndpointTests(ApiServiceInternalKeyHttpTests.ApiServiceHost host)
    {
        _host = host;
    }

    private sealed class FakeCompanies(CompanyLookupOutcome outcome, List<CompanySuggestionDto>? suggestions = null,
        CompanyPressReleasesOutcome? releases = null, bool releasesEnabled = true)
        : ICompanyProfileService
    {
        public string? LookedUp { get; private set; }
        public string? LookedUpBy { get; private set; }
        public string? Suggested { get; private set; }
        public string? ReleasesOf { get; private set; }
        public string? ReleasesFor { get; private set; }

        public bool PressReleasesEnabled => releasesEnabled;

        public Task<CompanyPressReleasesOutcome> GetPressReleasesAsync(string userId, string ticker,
            CancellationToken cancellationToken = default)
        {
            ReleasesFor = userId;
            ReleasesOf = ticker;
            return Task.FromResult(releases ?? ReleasesOutcome());
        }

        public Task<List<CompanySuggestionDto>?> SuggestAsync(string query, CancellationToken cancellationToken = default)
        {
            Suggested = query;
            return Task.FromResult(suggestions);
        }

        public Task<CompanyLookupOutcome> LookUpAsync(string userId, string ticker,
            CancellationToken cancellationToken = default)
        {
            LookedUpBy = userId;
            LookedUp = ticker;
            return Task.FromResult(outcome);
        }
    }

    private static CompaniesController Controller(ICompanyProfileService companies) =>
        new(companies)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "account-1")], "Test")),
                },
            },
        };

    private static CompanyLookupOutcome ProfileOutcome() =>
        CompanyLookupOutcome.Answer(CompanyLookupKinds.Profile, "MSFT", new CompanyProfileDto { Ticker = "MSFT" });

    private static CompanyPressReleasesOutcome ReleasesOutcome() =>
        CompanyPressReleasesOutcome.Answer(CompanyLookupKinds.Profile, "MSFT",
            [new CompanyPressReleaseDto { Headline = "Microsoft Cloud and AI Strength Fuels Fourth Quarter Results" }]);

    [Fact]
    public async Task Releases_NormalizesTheTicker_AndReadsThemForTheAccount()
    {
        var companies = new FakeCompanies(ProfileOutcome());

        IActionResult result = await Controller(companies).Releases(" brk.b ");

        CompanyPressReleasesResultDto body =
            Assert.IsType<CompanyPressReleasesResultDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(body.Releases);
        Assert.Equal("BRK-B", companies.ReleasesOf);
        Assert.Equal("account-1", companies.ReleasesFor);
    }

    [Theory]
    [InlineData(CompanyLookupKinds.Fund)]
    [InlineData(CompanyLookupKinds.NotFound)]
    public async Task Releases_OfAFundOrAnUnlistedTicker_Is200WithTheKind(string kind)
    {
        var companies = new FakeCompanies(ProfileOutcome(),
            releases: CompanyPressReleasesOutcome.Answer(kind, "MSFT"));

        IActionResult result = await Controller(companies).Releases("MSFT");

        Assert.Equal(kind,
            Assert.IsType<CompanyPressReleasesResultDto>(Assert.IsType<OkObjectResult>(result).Value).Kind);
    }

    [Theory]
    [InlineData("TOOLONGTICKER")]
    [InlineData("<b>")]
    public async Task Releases_ARejectedTicker_Is400_WithoutReadingThem(string ticker)
    {
        var companies = new FakeCompanies(ProfileOutcome());

        IActionResult result = await Controller(companies).Releases(ticker);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(companies.ReleasesOf);
    }

    [Fact]
    public async Task Releases_SwitchedOff_Is404_WithoutReadingThem()
    {
        var companies = new FakeCompanies(ProfileOutcome(), releasesEnabled: false);

        IActionResult result = await Controller(companies).Releases("MSFT");

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(companies.ReleasesOf);
    }

    [Fact]
    public async Task Releases_TooManyLookups_Is429()
    {
        var companies = new FakeCompanies(ProfileOutcome(), releases: CompanyPressReleasesOutcome.TooManyLookups());

        IActionResult result = await Controller(companies).Releases("MSFT");

        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Releases_SecUnavailable_Is503WithAReason()
    {
        var companies = new FakeCompanies(ProfileOutcome(), releases: CompanyPressReleasesOutcome.SecUnavailable());

        IActionResult result = await Controller(companies).Releases("MSFT");

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.Equal(CompaniesController.ProfileUnavailable,
            Assert.IsType<ResearchUnavailableDto>(objectResult.Value).Reason);
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        Assert.NotNull(typeof(CompaniesController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Get_NormalizesTheTicker_AndLooksItUpForTheAccount()
    {
        var companies = new FakeCompanies(ProfileOutcome());

        IActionResult result = await Controller(companies).Get(" brk.b ");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("BRK-B", companies.LookedUp);
        Assert.Equal("account-1", companies.LookedUpBy);
    }

    [Theory]
    [InlineData(CompanyLookupKinds.Profile)]
    [InlineData(CompanyLookupKinds.Fund)]
    [InlineData(CompanyLookupKinds.NotFound)]
    public async Task Get_AnAnswer_Is200WithIt(string kind)
    {
        CompanyLookupOutcome outcome = CompanyLookupOutcome.Answer(kind, "MSFT");

        IActionResult result = await Controller(new FakeCompanies(outcome)).Get("MSFT");

        Assert.Equal(kind, Assert.IsType<CompanyLookupResultDto>(Assert.IsType<OkObjectResult>(result).Value).Kind);
    }

    [Fact]
    public async Task Get_TooManyLookups_Is429()
    {
        IActionResult result = await Controller(new FakeCompanies(CompanyLookupOutcome.TooManyLookups())).Get("MSFT");

        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Get_SecUnavailable_Is503WithAReason()
    {
        IActionResult result = await Controller(new FakeCompanies(CompanyLookupOutcome.SecUnavailable())).Get("MSFT");

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.Equal(CompaniesController.ProfileUnavailable,
            Assert.IsType<ResearchUnavailableDto>(objectResult.Value).Reason);
    }

    [Theory]
    [InlineData("TOOLONGTICKER")]
    [InlineData("AB/C")]
    [InlineData("<b>")]
    public async Task Get_ARejectedTicker_Is400_WithoutALookup(string ticker)
    {
        var companies = new FakeCompanies(ProfileOutcome());

        IActionResult result = await Controller(companies).Get(ticker);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(companies.LookedUp);
    }

    [Fact]
    public async Task Suggest_ReturnsTheSuggestions_ForTheTrimmedQuery()
    {
        var companies = new FakeCompanies(ProfileOutcome(), [new CompanySuggestionDto { Ticker = "MSFT" }]);

        IActionResult result = await Controller(companies).Suggest("  micro ");

        Assert.Single(Assert.IsType<List<CompanySuggestionDto>>(Assert.IsType<OkObjectResult>(result).Value));
        Assert.Equal("micro", companies.Suggested);
    }

    [Fact]
    public async Task Suggest_WhenTheListCannotBeRead_Is503()
    {
        IActionResult result = await Controller(new FakeCompanies(ProfileOutcome(), suggestions: null)).Suggest("micro");

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 41 characters
    public async Task Suggest_ARejectedQuery_Is400(string? query)
    {
        var companies = new FakeCompanies(ProfileOutcome(), []);

        IActionResult result = await Controller(companies).Suggest(query);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(companies.Suggested);
    }

    [Theory]
    [InlineData("/api/Research/companies/MSFT")]
    [InlineData("/api/Research/companies/suggest?q=micro")]
    [InlineData("/api/Research/companies/MSFT/releases")]
    public async Task OverHttp_WithoutTheSharedSecret_IsRejected(string url)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-User-Id", "account-1");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/Research/companies/TOOLONGTICKER")]
    [InlineData("/api/Research/companies/suggest?q=")]
    [InlineData("/api/Research/companies/TOOLONGTICKER/releases")]
    public async Task OverHttp_SignedIn_ARejectedInput_Is400(string url)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
