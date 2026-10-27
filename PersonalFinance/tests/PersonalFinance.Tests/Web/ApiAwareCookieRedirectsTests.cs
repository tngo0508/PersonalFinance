using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Web;

public class ApiAwareCookieRedirectsTests
{
    [Theory]
    [InlineData("/api/Notification", null, null)]
    [InlineData("/Budget/Data", "application/json", null)]
    [InlineData("/Budget/Data", null, "XMLHttpRequest")]
    public async Task ScriptRequests_GetStatusCodesInsteadOfRedirects(string path, string? accept, string? requestedWith)
    {
        var events = new CookieAuthenticationEvents();
        ApiAwareCookieRedirects.Apply(events);

        RedirectContext<CookieAuthenticationOptions> login = CreateContext(path, accept, requestedWith);
        await events.OnRedirectToLogin(login);
        RedirectContext<CookieAuthenticationOptions> denied = CreateContext(path, accept, requestedWith);
        await events.OnRedirectToAccessDenied(denied);

        Assert.Equal(StatusCodes.Status401Unauthorized, login.Response.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.Response.StatusCode);
        Assert.False(login.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task PageNavigation_IsRedirected_ForBothLoginAndAccessDenied()
    {
        var events = new CookieAuthenticationEvents();
        ApiAwareCookieRedirects.Apply(events);

        RedirectContext<CookieAuthenticationOptions> login = CreateContext("/Dashboard", "text/html", null);
        await events.OnRedirectToLogin(login);
        RedirectContext<CookieAuthenticationOptions> denied = CreateContext("/Dashboard", "text/html", null);
        await events.OnRedirectToAccessDenied(denied);

        Assert.Equal(StatusCodes.Status302Found, login.Response.StatusCode);
        Assert.Equal("/Identity/Account/Login?ReturnUrl=%2FDashboard", login.Response.Headers.Location.ToString());
        Assert.Equal(StatusCodes.Status302Found, denied.Response.StatusCode);
        Assert.Equal("/Identity/Account/Login?ReturnUrl=%2FDashboard", denied.Response.Headers.Location.ToString());
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateContext(string path, string? accept,
        string? requestedWith)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        if (accept is not null)
        {
            httpContext.Request.Headers.Accept = accept;
        }

        if (requestedWith is not null)
        {
            httpContext.Request.Headers.XRequestedWith = requestedWith;
        }

        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null,
            typeof(CookieAuthenticationHandler));
        return new RedirectContext<CookieAuthenticationOptions>(httpContext, scheme, new CookieAuthenticationOptions(),
            new AuthenticationProperties(), "/Identity/Account/Login?ReturnUrl=%2FDashboard");
    }
}
