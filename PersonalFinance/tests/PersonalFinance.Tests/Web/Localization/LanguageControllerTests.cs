using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>The language switcher endpoint (spec §2.3 and the §5 security checks).</summary>
public class LanguageControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Set_IsAnAntiforgeryProtectedPost_OpenToAnonymousVisitors()
    {
        MethodInfo set = typeof(LanguageController).GetMethod(nameof(LanguageController.Set))!;

        Assert.NotNull(set.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(set.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(set.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(typeof(LanguageController).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task Set_SupportedLanguage_WritesTheCultureCookie_AndReturnsToThePage()
    {
        (LanguageController controller, HttpContext context, InMemoryUserPreferenceService _) = Create();

        IActionResult result = await controller.Set("vi", "/SavingsGoals?x=1");

        LocalRedirectResult redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/SavingsGoals?x=1", redirect.Url);
        string cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=Mon, 25 Oct 2027", cookie);
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example")]
    [InlineData(null)]
    public async Task Set_NonLocalReturnUrl_GoesHome(string? returnUrl)
    {
        (LanguageController controller, HttpContext _, InMemoryUserPreferenceService _) = Create();

        LocalRedirectResult redirect = Assert.IsType<LocalRedirectResult>(await controller.Set("en", returnUrl));

        Assert.Equal("~/", redirect.Url);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Set_UnsupportedLanguage_IsRejected_WithoutACookieOrAStoredChoice(string? language)
    {
        (LanguageController controller, HttpContext context, InMemoryUserPreferenceService preferences) =
            Create(signedInUserId: "user-1");

        Assert.IsType<BadRequestResult>(await controller.Set(language, "/"));
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Empty(preferences.Languages);
    }

    [Fact]
    public async Task Set_SignedIn_StoresTheChoiceForThatAccountOnly()
    {
        (LanguageController controller, HttpContext _, InMemoryUserPreferenceService preferences) =
            Create(signedInUserId: "user-1");

        await controller.Set("vi", "/");

        Assert.Equal(new Dictionary<string, string> { ["user-1"] = "vi" }, preferences.Languages);
    }

    [Fact]
    public async Task Set_Anonymous_StoresNothing()
    {
        (LanguageController controller, HttpContext _, InMemoryUserPreferenceService preferences) = Create();

        await controller.Set("vi", "/");

        Assert.Empty(preferences.Languages);
    }

    private static (LanguageController Controller, HttpContext Context, InMemoryUserPreferenceService Preferences)
        Create(string? signedInUserId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        if (signedInUserId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, signedInUserId)], "TestAuthType"));
        }

        var preferences = new InMemoryUserPreferenceService();
        var controller = new LanguageController(preferences, new FixedTimeProvider(Now))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        controller.Url = new UrlHelper(new ActionContext(context, new RouteData(), new ActionDescriptor()));
        return (controller, context, preferences);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
