using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Data;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// Real ASP.NET Core Identity over an InMemory <see cref="AppDbContext"/>, registered as <c>Program.cs</c> does
/// (<c>AddDefaultIdentity</c> with Entity Framework stores, roles on request), resolved in one scope that the test
/// disposes. Identity page tests attach a <see cref="PageModel"/> to an HTTPS request over that scope.
/// </summary>
public sealed class IdentityHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    private IdentityHarness(ServiceProvider provider, AsyncServiceScope scope)
    {
        _provider = provider;
        _scope = scope;
    }

    /// <summary>The scoped services: Identity's managers and stores, the database, logging.</summary>
    public IServiceProvider Services => _scope.ServiceProvider;

    public UserManager<IdentityUser> UserManager => Services.GetRequiredService<UserManager<IdentityUser>>();

    /// <summary>
    /// Logging, the test host environment, an InMemory database named <paramref name="databaseName"/> plus a unique
    /// suffix, and Identity, for tests that add registrations of their own before building the provider.
    /// </summary>
    public static IServiceCollection RegisterIdentity(string databaseName, bool withRoles = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment, TestWebHostEnvironment>();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"{databaseName}_{Guid.NewGuid():N}"));
        IdentityBuilder identity = services.AddDefaultIdentity<IdentityUser>();
        if (withRoles)
        {
            identity.AddRoles<IdentityRole>();
        }

        identity.AddEntityFrameworkStores<AppDbContext>();
        return services;
    }

    /// <summary>Builds <see cref="RegisterIdentity"/> and opens the scope the test works in.</summary>
    public static IdentityHarness Create(string databaseName, bool withRoles = false)
    {
        ServiceProvider provider = RegisterIdentity(databaseName, withRoles).BuildServiceProvider();
        return new IdentityHarness(provider, provider.CreateAsyncScope());
    }

    /// <summary>An HTTPS request over <see cref="Services"/>, signed in as <paramref name="user"/> when one is given.</summary>
    public DefaultHttpContext NewHttpContext(IdentityUser? user = null)
    {
        var httpContext = new DefaultHttpContext { RequestServices = Services };
        httpContext.Request.Scheme = "https";
        if (user is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)], IdentityConstants.ApplicationScheme));
        }

        return httpContext;
    }

    /// <summary>Gives <paramref name="page"/> a fresh anonymous request and <c>app.test</c> page links.</summary>
    public void Attach(PageModel page) => Attach(page, NewHttpContext());

    /// <summary>Gives <paramref name="page"/> the request <paramref name="httpContext"/> and <c>app.test</c> page links.</summary>
    public static void Attach(PageModel page, HttpContext httpContext)
    {
        page.PageContext = new PageContext { HttpContext = httpContext };
        page.Url = new PageUrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    /// <summary>
    /// Builds page links as <c>{protocol}://app.test/{area}{page}</c>, with <c>?token=...</c> when the route
    /// carries a token (the cancel-deletion link).
    /// </summary>
    private sealed class PageUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext)
        {
            var values = new RouteValueDictionary(routeContext.Values);
            string link = $"{routeContext.Protocol}://app.test/{values["area"]}{values["page"]}";
            return values.TryGetValue("token", out object? token) ? $"{link}?token={token}" : link;
        }
    }
}
