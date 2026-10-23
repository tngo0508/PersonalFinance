using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Home;
using Xunit;
using DashboardController = PersonalFinance.Web.Controllers.DashboardController;

namespace PersonalFinance.Tests.LandingPage;

public class HomeControllerTests
{
    private static readonly string[] ExpectedCoreFeatureTitles = ["Net Worth", "Budget", "Savings Goals"];

    private static readonly string[] ExpectedMoreFeatureTitles =
    [
        "Spreadsheet Analysis", "Investment Calculator", "Google Drive Integration", "AI Assistant",
        "Finance News", "Community"
    ];

    private static ControllerContext CreateAnonymousContext()
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    private static ControllerContext CreateAuthenticatedContext(string userId = "test-user-123")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"{userId}@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    [Fact]
    public void Index_WhenVisitorIsSignedOut_ReturnsLandingPage()
    {
        // Arrange
        var controller = new HomeController
        {
            ControllerContext = CreateAnonymousContext()
        };

        // Act
        IActionResult result = controller.Index();

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Null(viewResult.ViewName);
        LandingPageViewModel model = Assert.IsType<LandingPageViewModel>(viewResult.Model);
        Assert.Equal(ExpectedCoreFeatureTitles, model.CoreFeatures.Select(f => f.Title));
        Assert.Equal(ExpectedMoreFeatureTitles, model.MoreFeatures.Select(f => f.Title));
    }

    [Fact]
    public void Index_WhenUserIsSignedIn_RedirectsToDashboard()
    {
        // Arrange
        var controller = new HomeController
        {
            ControllerContext = CreateAuthenticatedContext()
        };

        // Act
        IActionResult result = controller.Index();

        // Assert
        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void About_RendersTheAboutPageForSignedOutAndSignedInVisitors(bool signedIn)
    {
        // Arrange
        var controller = new HomeController
        {
            ControllerContext = signedIn ? CreateAuthenticatedContext() : CreateAnonymousContext()
        };

        // Act
        IActionResult result = controller.About();

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Null(viewResult.ViewName);
    }

    [Fact]
    public void About_IsServedAtTheAboutRouteWithoutSignIn()
    {
        MethodInfo method = typeof(HomeController).GetMethod(nameof(HomeController.About))!;

        HttpGetAttribute route = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal("/about", route.Template);
        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void HomeController_IsOpenToAnonymousVisitors()
    {
        Assert.NotNull(typeof(HomeController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(typeof(HomeController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void DashboardController_StillRequiresSignIn()
    {
        Assert.NotNull(typeof(DashboardController).GetCustomAttribute<AuthorizeAttribute>());
    }
}
