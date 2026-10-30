using System.Net;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Learning;
using ApiException = Refit.ApiException;
using LearningController = PersonalFinance.Web.Controllers.LearningController;
using RefitSettings = Refit.RefitSettings;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// The Web Atelier pages (#517, #519, #520): the Library and the Journey's road in the reader's Language, a Term Entry
/// with its canonical URL, a Lesson as a guest or a signed-in Account sees it with its Review, finishing a Lesson,
/// marking a Review card, a 404 for an unknown slug, and the sitemap and robots.txt a search engine reads.
/// </summary>
public class LearningWebControllerTests
{
    private static readonly LearningLibraryDto Library = new()
    {
        Topics =
        [
            new LearningTopicDto { Slug = "saving-banking", Terms = [new TermSummaryDto { Slug = "emergency-fund" }] },
            new LearningTopicDto
            {
                Slug = "borrowing-credit",
                Terms = [new TermSummaryDto { Slug = "credit-score" }, new TermSummaryDto { Slug = "apr" }]
            },
            new LearningTopicDto { Slug = "tax" },
        ],
    };

    [Fact]
    public async Task Index_AsksForTheReadersLanguage_AndShowsTheShelves()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLibraryAsync), Library);
        proxy.Returns(nameof(ILearningApi.GetLessonsAsync), Outline);

        IActionResult result;
        using (new UiCultureScope("vi"))
        {
            result = await Controller(api).Index();
        }

        LearningIndexViewModel model = Assert.IsType<LearningIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.IsEmpty);
        Assert.Equal("vi", proxy.Arguments[0][0]);
    }

    [Fact]
    public async Task Index_ForAGuest_ShowsTheOutlineWithoutStatuses()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLibraryAsync), Library);
        proxy.Returns(nameof(ILearningApi.GetLessonsAsync), Outline);

        LearningIndexViewModel model = Model<LearningIndexViewModel>(await Controller(api).Index());

        Assert.False(model.IsSignedIn);
        Assert.Equal(["build-a-cushion", "borrow-wisely"], model.Road.Select(s => s.Slug));
        Assert.All(model.Road, s => Assert.Null(s.Status));
        Assert.DoesNotContain(nameof(ILearningApi.GetJourneyAsync), proxy.Calls);
    }

    [Fact]
    public async Task Index_SignedIn_ShowsTheJourneyWithTheKeepsakesOnTheDesk()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLibraryAsync), Library);
        proxy.Returns(nameof(ILearningApi.GetJourneyAsync), FinishedDayOne);

        LearningIndexViewModel model = Model<LearningIndexViewModel>(await Controller(api, signedIn: true).Index());

        Assert.True(model.IsSignedIn);
        Assert.Equal([LessonStatus.Finished, LessonStatus.Locked], model.Road.Select(s => s.Status));
        Assert.True(model.Road[1].OpensTomorrow);
        Assert.Equal(["Coin jar"], model.Keepsakes.Select(k => k.Name));
        Assert.DoesNotContain(nameof(ILearningApi.GetLessonsAsync), proxy.Calls);
    }

    [Fact]
    public async Task Index_SignedIn_CarriesTheDashboardPreference_ForTheSwitch()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLibraryAsync), Library);
        proxy.Returns(nameof(ILearningApi.GetJourneyAsync),
            new JourneyStateDto { Started = true, ShowOnDashboard = true, Lessons = DayTwoOpen.Lessons });

        LearningIndexViewModel model = Model<LearningIndexViewModel>(await Controller(api, signedIn: true).Index());

        Assert.True(model.ShowOnDashboard);
    }

    [Theory]
    [InlineData(false, "/Dashboard", "/Dashboard")]
    [InlineData(true, "/Learning#journey", "/Learning#journey")]
    public async Task ShowOnDashboard_SavesThePreference_AndGoesBack(bool show, string returnUrl, string expected)
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.SetShowOnDashboardAsync), null);

        IActionResult result = await Controller(api, signedIn: true).ShowOnDashboard(show, returnUrl);

        Assert.Equal(expected, Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Equal(show, Assert.IsType<ShowOnDashboardRequest>(proxy.Arguments.Single()[0]).Show);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    public async Task ShowOnDashboard_WithoutALocalReturnUrl_GoesToTheJourney(string? returnUrl)
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.SetShowOnDashboardAsync), null);

        IActionResult result = await Controller(api, signedIn: true).ShowOnDashboard(true, returnUrl);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(("Index", "Learning", "journey"), (redirect.ActionName, redirect.ControllerName, redirect.Fragment));
    }

    [Fact]
    public async Task ShowOnDashboard_WhenApiServiceFails_StillGoesBack()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Throws(nameof(ILearningApi.SetShowOnDashboardAsync), new HttpRequestException("connection refused"));

        IActionResult result = await Controller(api, signedIn: true).ShowOnDashboard(false, "/Dashboard");

        Assert.Equal("/Dashboard", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Fact]
    public async Task Lesson_ForAGuest_AsksToSignIn()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLessonAsync), Lesson("borrow-wisely", 2));

        LearningLessonViewModel model = Model<LearningLessonViewModel>(await Controller(api).Lesson("borrow-wisely"));

        Assert.Equal(LessonAccess.SignIn, model.Access);
        Assert.DoesNotContain(nameof(ILearningApi.GetJourneyAsync), proxy.Calls);
    }

    [Fact]
    public async Task Lesson_SignedIn_LockedUntilTomorrow()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLessonAsync), Lesson("borrow-wisely", 2));
        proxy.Returns(nameof(ILearningApi.GetJourneyAsync), FinishedDayOne);

        LearningLessonViewModel model =
            Model<LearningLessonViewModel>(await Controller(api, signedIn: true).Lesson("borrow-wisely"));

        Assert.Equal(LessonAccess.Locked, model.Access);
        Assert.True(model.OpensTomorrow);
        Assert.Equal("build-a-cushion", model.Previous!.Slug);
    }

    [Fact]
    public async Task Lesson_OpenOnDayTwo_OpensWithTheReviewInTheReadersLanguage()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLessonAsync), Lesson("borrow-wisely", 2));
        proxy.Returns(nameof(ILearningApi.GetJourneyAsync), DayTwoOpen);
        proxy.Returns(nameof(ILearningApi.GetReviewAsync), new ReviewDto { Cards = [new() { Slug = "emergency-fund" }] });

        LearningLessonViewModel model;
        using (new UiCultureScope("vi"))
        {
            model = Model<LearningLessonViewModel>(await Controller(api, signedIn: true).Lesson("borrow-wisely"));
        }

        Assert.Equal("emergency-fund", Assert.Single(model.Review).Slug);
        Assert.Equal("vi", proxy.Arguments[proxy.Calls.IndexOf(nameof(ILearningApi.GetReviewAsync))][0]);
    }

    [Fact]
    public async Task Lesson_Locked_DoesNotAskForTheReview()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLessonAsync), Lesson("borrow-wisely", 2));
        proxy.Returns(nameof(ILearningApi.GetJourneyAsync), FinishedDayOne);

        await Controller(api, signedIn: true).Lesson("borrow-wisely");

        Assert.DoesNotContain(nameof(ILearningApi.GetReviewAsync), proxy.Calls);
    }

    [Fact]
    public async Task Lesson_WhenTheReviewFails_OpensWithoutIt()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLessonAsync), Lesson("borrow-wisely", 2));
        proxy.Returns(nameof(ILearningApi.GetJourneyAsync), DayTwoOpen);
        proxy.Throws(nameof(ILearningApi.GetReviewAsync), new HttpRequestException("connection refused"));

        LearningLessonViewModel model =
            Model<LearningLessonViewModel>(await Controller(api, signedIn: true).Lesson("borrow-wisely"));

        Assert.Equal(LessonAccess.Open, model.Access);
        Assert.False(model.ShowsReview);
    }

    [Fact]
    public async Task MarkCard_PostsTheMark_AndAnswersNoContent()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.MarkReviewCardAsync), null);

        IActionResult result = await Controller(api, signedIn: true)
            .MarkCard("emergency-fund", new MarkReviewCardRequest { KnewIt = true });

        Assert.IsType<NoContentResult>(result);
        object?[] args = proxy.Arguments.Single();
        Assert.Equal("emergency-fund", args[0]);
        Assert.True(Assert.IsType<MarkReviewCardRequest>(args[1]).KnewIt);
    }

    [Fact]
    public async Task MarkCard_ATermNotYetLearned_IsNotFound()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Throws(nameof(ILearningApi.MarkReviewCardAsync), await ApiFailure(HttpStatusCode.NotFound));

        Assert.IsType<NotFoundResult>(await Controller(api, signedIn: true).MarkCard("apr", null));
    }

    [Fact]
    public async Task Lesson_Unknown_IsNotFound()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Throws(nameof(ILearningApi.GetLessonAsync), await ApiFailure(HttpStatusCode.NotFound));

        Assert.IsType<NotFoundResult>(await Controller(api).Lesson("no-such-lesson"));
    }

    [Fact]
    public async Task Finish_PostsTheBrowsersTimeZone_AndAnswersNoContent()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.FinishLessonAsync), FinishedDayOne);

        IActionResult result = await Controller(api, signedIn: true)
            .Finish("build-a-cushion", new FinishLessonRequest { TimeZone = "Asia/Ho_Chi_Minh" });

        Assert.IsType<NoContentResult>(result);
        object?[] args = proxy.Arguments.Single();
        Assert.Equal("build-a-cushion", args[0]);
        Assert.Equal("Asia/Ho_Chi_Minh", Assert.IsType<FinishLessonRequest>(args[1]).TimeZone);
    }

    [Fact]
    public async Task Finish_ALockedLesson_IsAConflict()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Throws(nameof(ILearningApi.FinishLessonAsync), await ApiFailure(HttpStatusCode.Conflict));

        IActionResult result = await Controller(api, signedIn: true).Finish("borrow-wisely", null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task Index_WithAnEmptyLibrary_IsEmpty()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLibraryAsync), new LearningLibraryDto
        {
            Topics = [new LearningTopicDto { Slug = "tax" }]
        });
        proxy.Returns(nameof(ILearningApi.GetLessonsAsync), new List<LessonSummaryDto>());

        IActionResult result = await Controller(api).Index();

        Assert.True(Assert.IsType<LearningIndexViewModel>(Assert.IsType<ViewResult>(result).Model).IsEmpty);
    }

    [Fact]
    public async Task Index_WhenApiServiceFails_ShowsTheUnavailableState()
    {
        (ILearningApi api, _) = ApiProxy.Create<ILearningApi>();

        IActionResult result = await Controller(api).Index();

        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, Assert.IsType<ViewResult>(result).ViewName);
    }

    [Fact]
    public async Task Term_ShowsTheEntry_WithItsCanonicalUrl()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetTermAsync), new TermEntryDto { Slug = "apr", Term = "Annual percentage rate" });

        IActionResult result = await Controller(api).Term("apr");

        LearningTermViewModel model = Assert.IsType<LearningTermViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("https://pf.test/Learning/Term/apr", model.CanonicalUrl);
        Assert.Equal(["apr", "en"], proxy.Arguments[0].Take(2));
    }

    [Fact]
    public async Task Term_UnknownSlug_IsNotFound()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Throws(nameof(ILearningApi.GetTermAsync), await ApiFailure(HttpStatusCode.NotFound));

        Assert.IsType<NotFoundResult>(await Controller(api).Term("mortgage"));
    }

    [Fact]
    public async Task Sitemap_ListsTheLibraryAndEveryEntry()
    {
        (ILearningApi api, ApiProxy<ILearningApi> proxy) = ApiProxy.Create<ILearningApi>();
        proxy.Returns(nameof(ILearningApi.GetLibraryAsync), Library);

        ContentResult result = Assert.IsType<ContentResult>(await Controller(api).Sitemap());

        Assert.StartsWith("application/xml", result.ContentType);
        Assert.Equal(["https://pf.test/Learning/Index", "https://pf.test/Learning/Term/apr",
                "https://pf.test/Learning/Term/credit-score", "https://pf.test/Learning/Term/emergency-fund"],
            Locations(result.Content!));
        Assert.Equal("en", proxy.Arguments[0][0]);
    }

    [Fact]
    public async Task Sitemap_WhenApiServiceFails_ListsOnlyTheLibrary()
    {
        (ILearningApi api, _) = ApiProxy.Create<ILearningApi>();

        ContentResult result = Assert.IsType<ContentResult>(await Controller(api).Sitemap());

        Assert.Equal(["https://pf.test/Learning/Index"], Locations(result.Content!));
    }

    [Fact]
    public void Robots_PointsAtTheSitemap()
    {
        (ILearningApi api, _) = ApiProxy.Create<ILearningApi>();

        ContentResult result = Assert.IsType<ContentResult>(Controller(api).Robots());

        Assert.StartsWith("text/plain", result.ContentType);
        Assert.Contains("User-agent: *", result.Content);
        Assert.Contains("Sitemap: https://pf.test/Learning/Sitemap", result.Content);
    }

    private static List<string> Locations(string xml)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        return XDocument.Parse(xml).Descendants(ns + "loc").Select(e => e.Value).ToList();
    }

    private static readonly List<LessonSummaryDto> Outline =
    [
        new() { Slug = "borrow-wisely", Day = 2, Title = "Borrow wisely" },
        new() { Slug = "build-a-cushion", Day = 1, Title = "Build a cushion" },
    ];

    // Lesson 1 finished today; Lesson 2 opens at the next local midnight.
    private static readonly JourneyStateDto FinishedDayOne = new()
    {
        Started = true,
        Lessons =
        [
            new LessonProgressDto
            {
                Slug = "build-a-cushion", Day = 1, Status = LessonStatus.Finished,
                FinishedAtUtc = new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc),
                Keepsake = new KeepsakeDto { Symbol = "🫙", Name = "Coin jar" },
            },
            new LessonProgressDto
            {
                Slug = "borrow-wisely", Day = 2, Status = LessonStatus.Locked,
                OpensAtUtc = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
                Keepsake = new KeepsakeDto { Symbol = "📒", Name = "Ledger" },
            },
        ],
    };

    // Lesson 1 finished two days ago; Lesson 2 is open today.
    private static readonly JourneyStateDto DayTwoOpen = new()
    {
        Started = true,
        Lessons =
        [
            new LessonProgressDto { Slug = "build-a-cushion", Day = 1, Status = LessonStatus.Finished },
            new LessonProgressDto { Slug = "borrow-wisely", Day = 2, Status = LessonStatus.Open },
        ],
    };

    private static LessonDto Lesson(string slug, int day) => new() { Slug = slug, Day = day, Title = slug };

    private static T Model<T>(IActionResult result) =>
        Assert.IsType<T>(Assert.IsType<ViewResult>(result).Model);

    private static LearningController Controller(ILearningApi api, bool signedIn = false) =>
        new(api, NullLogger<LearningController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = signedIn
                        ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"))
                        : new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
            Url = new FakeUrlHelper(),
        };

    private static async Task<ApiException> ApiFailure(HttpStatusCode status) =>
        await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/learning/terms/x"),
            HttpMethod.Get, new HttpResponseMessage(status) { Content = new StringContent("{}", Encoding.UTF8) },
            new RefitSettings());

    /// <summary>Builds <c>https://pf.test/{controller}/{action}[/{slug}]</c>.</summary>
    private sealed class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext)
        {
            string? slug = actionContext.Values?.GetType().GetProperty("slug")?.GetValue(actionContext.Values) as string;
            return $"https://pf.test/{actionContext.Controller}/{actionContext.Action}" + (slug is null ? "" : "/" + slug);
        }

        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) =>
            url is not null && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal);
        public string? Link(string? routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }
}
