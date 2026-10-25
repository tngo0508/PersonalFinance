using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
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
    public void Set_SupportedLanguage_WritesTheCultureCookie_AndReturnsToThePage()
    {
        (LanguageController controller, HttpContext context) = Create();

        IActionResult result = controller.Set("vi", "/SavingsGoals?x=1");

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
    public void Set_NonLocalReturnUrl_GoesHome(string? returnUrl)
    {
        (LanguageController controller, HttpContext _) = Create();

        LocalRedirectResult redirect = Assert.IsType<LocalRedirectResult>(controller.Set("en", returnUrl));

        Assert.Equal("~/", redirect.Url);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public void Set_UnsupportedLanguage_IsRejected_WithoutACookie(string? language)
    {
        (LanguageController controller, HttpContext context) = Create();

        Assert.IsType<BadRequestResult>(controller.Set(language, "/"));
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    private static (LanguageController Controller, HttpContext Context) Create()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var controller = new LanguageController(new FixedTimeProvider(Now))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        controller.Url = new UrlHelper(new ActionContext(context, new RouteData(), new ActionDescriptor()));
        return (controller, context);
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
