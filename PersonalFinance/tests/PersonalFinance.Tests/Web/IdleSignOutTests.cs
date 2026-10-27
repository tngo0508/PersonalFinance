using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalFinance.Web.Areas.Identity.Pages.Account;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// Idle Sign-out (#332): the sign-in cookie lives as long as <c>IdleSignOut:TimeoutMinutes</c> and slides, the
/// browser's check-in renews it, and no sign-in outlives the browser through "Remember me".
/// </summary>
public class IdleSignOutTests
{
    [Fact]
    public void CookieLifetime_ComesFromConfig_WithSlidingExpiration()
    {
        using ServiceProvider services = BuildServices(new Dictionary<string, string?>
        {
            ["IdleSignOut:TimeoutMinutes"] = "7",
            ["IdleSignOut:WarningSeconds"] = "20"
        });

        CookieAuthenticationOptions cookie = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        IdleSignOutOptions idle = services.GetRequiredService<IOptions<IdleSignOutOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(7), cookie.ExpireTimeSpan);
        Assert.True(cookie.SlidingExpiration);
        Assert.Equal(7, idle.TimeoutMinutes);
        Assert.Equal(20, idle.WarningSeconds);
    }

    [Fact]
    public void WithoutConfig_TheTimeoutIsThirtyMinutes_AndTheWarningTwoMinutes()
    {
        using ServiceProvider services = BuildServices([]);

        CookieAuthenticationOptions cookie = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        IdleSignOutOptions idle = services.GetRequiredService<IOptions<IdleSignOutOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(30), cookie.ExpireTimeSpan);
        Assert.Equal(120, idle.WarningSeconds);
    }

    [Fact]
    public void WebAppSettings_SetThirtyMinutes_AndTwoMinutesOfWarning()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(TestSupport.RepositoryRoot.SourceProject("PersonalFinance.Web"), "appsettings.json"))
            .Build();

        Assert.Equal("30", configuration["IdleSignOut:TimeoutMinutes"]);
        Assert.Equal("120", configuration["IdleSignOut:WarningSeconds"]);
    }

    [Theory]
    [InlineData("0", "120")]
    [InlineData("1", "0")]
    [InlineData("1", "60")]
    public void AWarningThatDoesNotFitInsideTheTimeout_IsRejected(string timeoutMinutes, string warningSeconds)
    {
        using ServiceProvider services = BuildServices(new Dictionary<string, string?>
        {
            ["IdleSignOut:TimeoutMinutes"] = timeoutMinutes,
            ["IdleSignOut:WarningSeconds"] = warningSeconds
        });

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<IdleSignOutOptions>>().Value);
    }

    [Fact]
    public async Task CheckIn_RenewsTheCookie()
    {
        CookieValidatePrincipalContext context = await ValidateAsync(HttpMethods.Post, IdleSignOutController.CheckInPath);

        Assert.True(context.ShouldRenew);
    }

    [Theory]
    [InlineData("POST", "/Budget/Save")]
    [InlineData("GET", "/idle-sign-out/check-in")]
    [InlineData("GET", "/api/Notification")]
    public async Task OtherRequests_LeaveRenewalToSlidingExpiration(string method, string path)
    {
        CookieValidatePrincipalContext context = await ValidateAsync(method, path);

        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task CheckIn_StillRunsIdentitysOwnValidation_AndNeverRenewsARejectedPrincipal()
    {
        var events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = context =>
            {
                context.RejectPrincipal();
                return Task.CompletedTask;
            }
        };
        IdleSignOutCookie.RenewOnCheckIn(events);

        CookieValidatePrincipalContext context = CreateValidateContext(HttpMethods.Post, IdleSignOutController.CheckInPath);
        await events.OnValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public void CheckInEndpoint_RequiresSignIn_AndTheAntiforgeryToken()
    {
        MethodInfo checkIn = typeof(IdleSignOutController).GetMethod(nameof(IdleSignOutController.CheckIn))!;

        Assert.NotNull(typeof(IdleSignOutController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(IdleSignOutController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(checkIn.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(checkIn.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        HttpPostAttribute post = Assert.Single(checkIn.GetCustomAttributes<HttpPostAttribute>());
        RouteAttribute route = typeof(IdleSignOutController).GetCustomAttribute<RouteAttribute>()!;
        Assert.Equal(IdleSignOutController.CheckInPath, $"/{route.Template}/{post.Template}");
    }

    [Fact]
    public void CheckIn_AnswersNoContent()
    {
        var controller = new IdleSignOutController();

        Assert.IsType<NoContentResult>(controller.CheckIn());
    }

    [Fact]
    public void Login_HasNoRememberMe()
    {
        Assert.Null(typeof(LoginModel.InputModel).GetProperty("RememberMe"));
        Assert.Null(typeof(LoginWith2faModel).GetProperty("RememberMe"));
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddIdleSignOut(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<CookieValidatePrincipalContext> ValidateAsync(string method, string path)
    {
        var events = new CookieAuthenticationEvents();
        IdleSignOutCookie.RenewOnCheckIn(events);
        CookieValidatePrincipalContext context = CreateValidateContext(method, path);
        await events.OnValidatePrincipal(context);
        return context;
    }

    private static CookieValidatePrincipalContext CreateValidateContext(string method, string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
        var scheme = new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme);
        return new CookieValidatePrincipalContext(httpContext, scheme, new CookieAuthenticationOptions(), ticket);
    }
}
