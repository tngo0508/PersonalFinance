using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Research;
using ResearchController = PersonalFinance.Web.Controllers.ResearchController;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Web side of the Companies tab: the page can open on any tab, and on the Companies tab on a ticker, from the
/// address; the Suggest and Company actions check input before calling ApiService and answer JavaScript with JSON.
/// </summary>
public class ResearchCompaniesWebControllerTests
{
    private static ResearchController CreateController(IResearchApi api, IConfiguration? configuration = null) =>
        new(api, configuration ?? new ConfigurationBuilder().Build(), NullLogger<ResearchController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ResearchViewModel ViewModelOf(IActionResult result) =>
        Assert.IsType<ResearchViewModel>(Assert.IsType<ViewResult>(result).Model);

    [Fact]
    public async Task Index_WithNoTab_OpensOnSectors()
    {
        (IResearchApi api, _) = ApiProxy.Create<IResearchApi>();

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.Equal(ResearchViewModel.SectorsTab, model.SelectedTab);
        Assert.Null(model.CompanyTicker);
    }

    [Theory]
    [InlineData("etfs", ResearchViewModel.EtfsTab)]
    [InlineData("WORLD", ResearchViewModel.WorldTab)]
    [InlineData("companies", ResearchViewModel.CompaniesTab)]
    [InlineData("unknown", ResearchViewModel.SectorsTab)]
    public async Task Index_OpensOnTheNamedTab(string tab, string expected)
    {
        (IResearchApi api, _) = ApiProxy.Create<IResearchApi>();

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index(tab));

        Assert.Equal(expected, model.SelectedTab);
    }

    [Fact]
    public async Task Index_OnTheCompaniesTab_OpensOnTheTicker_AsSecListsIt()
    {
        (IResearchApi api, _) = ApiProxy.Create<IResearchApi>();

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index("companies", "brk.b"));

        Assert.Equal("BRK-B", model.CompanyTicker);
    }

    [Theory]
    [InlineData("sectors", "MSFT")] // A ticker belongs to the Companies tab only.
    [InlineData("companies", "<script>")]
    [InlineData("companies", "TOOLONGTICKER")]
    public async Task Index_IgnoresATickerItCannotUse(string tab, string ticker)
    {
        (IResearchApi api, _) = ApiProxy.Create<IResearchApi>();

        Assert.Null(ViewModelOf(await CreateController(api).Index(tab, ticker)).CompanyTicker);
    }

    [Fact]
    public async Task Suggest_ReturnsApiServicesSuggestions_AsJson()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        List<CompanySuggestionDto> suggestions = [new() { Ticker = "MSFT", Name = "Microsoft Corp" }];
        proxy.Returns(nameof(IResearchApi.SuggestCompaniesAsync), suggestions);

        IActionResult result = await CreateController(api).Suggest(" micro ");

        Assert.Same(suggestions, Assert.IsType<JsonResult>(result).Value);
    }

    [Fact]
    public async Task Company_ReturnsApiServicesLookup_AsJson()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        var lookup = new CompanyLookupResultDto { Kind = CompanyLookupKinds.Fund, Ticker = "SPY" };
        proxy.Returns(nameof(IResearchApi.GetCompanyAsync), lookup);

        IActionResult result = await CreateController(api).Company("spy");

        Assert.Same(lookup, Assert.IsType<JsonResult>(result).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 41 characters
    public async Task Suggest_ARejectedQuery_Is400_WithoutCallingApiService(string? query)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();

        IActionResult result = await CreateController(api).Suggest(query);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(proxy.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("AB/C")]
    [InlineData("TOOLONGTICKER")]
    public async Task Company_ARejectedTicker_Is400_WithoutCallingApiService(string? ticker)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();

        IActionResult result = await CreateController(api).Company(ticker);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(proxy.Calls);
    }

    [Fact]
    public async Task Releases_ReturnsApiServicesPressReleases_AsJson()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        var releases = new CompanyPressReleasesResultDto
        {
            Kind = CompanyLookupKinds.Profile,
            Ticker = "BRK-B",
            Releases = [new CompanyPressReleaseDto { Headline = "Berkshire Hathaway Annual Meeting Results" }],
        };
        proxy.Returns(nameof(IResearchApi.GetCompanyPressReleasesAsync), releases);

        IActionResult result = await CreateController(api).Releases("brk.b");

        Assert.Same(releases, Assert.IsType<JsonResult>(result).Value);
        Assert.Equal([nameof(IResearchApi.GetCompanyPressReleasesAsync)], proxy.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("AB/C")]
    [InlineData("TOOLONGTICKER")]
    public async Task Releases_ARejectedTicker_Is400_WithoutCallingApiService(string? ticker)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();

        IActionResult result = await CreateController(api).Releases(ticker);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(proxy.Calls);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task Index_CarriesThePressReleasesFlag(string? setting, bool expected)
    {
        (IResearchApi api, _) = ApiProxy.Create<IResearchApi>();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Research:PressReleases:Enabled"] = setting })
            .Build();

        ResearchViewModel model = ViewModelOf(await CreateController(api, configuration).Index("companies", "MSFT"));

        Assert.Equal(expected, model.PressReleasesEnabled);
    }

    [Theory]
    [InlineData(nameof(ResearchController.Suggest))]
    [InlineData(nameof(ResearchController.Company))]
    [InlineData(nameof(ResearchController.Releases))]
    public void CompanyActions_MapApiServiceFailuresToJsonErrors(string action)
    {
        // JsonApiErrorsFilter keeps ApiService's status code (429, 503) with a safe message, and never the detail.
        MethodInfo method = typeof(ResearchController).GetMethod(action)!;

        Assert.NotNull(method.GetCustomAttribute<JsonApiErrorsAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
    }
}
