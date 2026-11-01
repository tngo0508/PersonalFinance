using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.Controllers;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Companies tab's AJAX endpoints answer a rejected ticker or query in the request's Language: the error is chrome
/// the tab shows in place of a profile, so it comes from the localizer, never hardcoded English.
/// </summary>
public class ResearchControllerLocalizationTests
{
    private const string English = """
        { "Research.Message.InvalidCompanyInput": "Enter a ticker or company name." }
        """;

    private const string Vietnamese = """
        { "Research.Message.InvalidCompanyInput": "Nhập mã hoặc tên công ty." }
        """;

    [Fact]
    public async Task Suggest_WithARejectedQuery_ReturnsTheErrorInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");

        IActionResult result = await CreateController().Suggest("   ");

        Assert.Equal("Nhập mã hoặc tên công ty.", ErrorOf(result));
    }

    [Fact]
    public async Task Company_WithARejectedTicker_ReturnsTheErrorInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("en");

        IActionResult result = await CreateController().CompanyProfile("not a ticker!");

        Assert.Equal("Enter a ticker or company name.", ErrorOf(result));
    }

    private static ResearchController CreateController()
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();
        return new ResearchController(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance,
            LocalizationTestCatalog.Localizer(English, Vietnamese))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static string? ErrorOf(IActionResult result)
    {
        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        return json.RootElement.GetProperty("error").GetString();
    }
}
