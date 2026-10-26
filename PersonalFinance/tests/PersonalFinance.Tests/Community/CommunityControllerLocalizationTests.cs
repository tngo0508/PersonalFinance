using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.Controllers;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Community page's AJAX endpoints answer in the request's Language: their error text is chrome the page shows in a
/// toast, so it comes from the localizer, never hardcoded English.
/// </summary>
public class CommunityControllerLocalizationTests
{
    private const string English = """
        {
          "Community.Message.PollRequired": "Poll ID is required.",
          "Community.Message.CommentRequired": "Comment text is required."
        }
        """;

    private const string Vietnamese = """
        {
          "Community.Message.PollRequired": "Cần mã cuộc thăm dò.",
          "Community.Message.CommentRequired": "Cần nhập nội dung bình luận."
        }
        """;

    [Fact]
    public async Task Vote_WithoutPoll_ReturnsTheErrorInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        CommunityController controller = CreateController();

        IActionResult result = await controller.Vote(new CommunityController.VoteRequest { OptionId = 1 });

        Assert.Equal("Cần mã cuộc thăm dò.", ErrorOf(result));
    }

    [Fact]
    public async Task AddComment_WithoutText_ReturnsTheErrorInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("en");
        CommunityController controller = CreateController();

        IActionResult result = await controller.AddComment(new CommunityController.CommentRequest { Text = " " });

        Assert.Equal("Comment text is required.", ErrorOf(result));
    }

    private static CommunityController CreateController()
    {
        (ICommunityApi? api, ApiProxy<ICommunityApi> _) = ApiProxy.Create<ICommunityApi>();
        var controller = new CommunityController(api, NullLogger<CommunityController>.Instance,
            LocalizationTestCatalog.Localizer(English, Vietnamese));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "community-user") }, "TestAuthType")),
            },
        };
        return controller;
    }

    private static string? ErrorOf(IActionResult result)
    {
        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        return json.RootElement.GetProperty("error").GetString();
    }
}
