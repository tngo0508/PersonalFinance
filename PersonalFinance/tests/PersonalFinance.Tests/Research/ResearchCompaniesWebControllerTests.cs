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
/// The Web side of the company lookup: the Suggest and CompanyProfile actions check input before calling ApiService and
/// answer JavaScript with JSON.
/// </summary>
public class ResearchCompaniesWebControllerTests
{
    private static ResearchController CreateController(IResearchApi api) =>
        new(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

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

        IActionResult result = await CreateController(api).CompanyProfile("spy");

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

        IActionResult result = await CreateController(api).CompanyProfile(ticker);

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

    [Fact]
    public void Controller_ReadsNoConfiguration_ApiServiceAloneSwitchesPressReleasesOff()
    {
        // The lookup's PressReleasesAvailable tells the page whether to show the section, so an operator switches
        // press releases off in one place, ApiService's Research:PressReleases:Enabled.
        Assert.All(typeof(ResearchController).GetConstructors(),
            c => Assert.DoesNotContain(c.GetParameters(), p => p.ParameterType == typeof(IConfiguration)));
    }

    [Theory]
    [InlineData(nameof(ResearchController.Suggest))]
    [InlineData(nameof(ResearchController.CompanyProfile))]
    [InlineData(nameof(ResearchController.Releases))]
    public void CompanyActions_MapApiServiceFailuresToJsonErrors(string action)
    {
        // JsonApiErrorsFilter keeps ApiService's status code (429, 503) with a safe message, and never the detail.
        MethodInfo method = typeof(ResearchController).GetMethod(action)!;

        Assert.NotNull(method.GetCustomAttribute<JsonApiErrorsAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
    }
}
