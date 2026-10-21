using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models;
using Xunit;

namespace PersonalFinance.Tests.Web;

public class HomeControllerTests
{
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
        var result = controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Null(viewResult.ViewName);
        var model = Assert.IsType<LandingPageViewModel>(viewResult.Model);
        Assert.Equal(
            new[]
            {
                "Net Worth", "Budget", "Savings Goals", "Spreadsheet Analysis", "Investment Calculator",
                "Finance News", "Community", "Google Drive Integration", "AI Assistant"
            },
            model.Features.Select(f => f.Title));
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
        var result = controller.Index();

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
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
