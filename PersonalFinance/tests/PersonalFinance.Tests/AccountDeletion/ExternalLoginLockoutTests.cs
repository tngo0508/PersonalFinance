using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Web.Areas.Identity.Pages.Account;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.AccountDeletion;

/// <summary>
/// A locked-out Account (for example one with a pending Deletion Request) cannot get back in through Google sign-in:
/// neither with a linked Google login nor by having one linked to it by email.
/// </summary>
public class ExternalLoginLockoutTests
{
    private const string Email = "leaving@example.com";

    [Fact]
    public async Task GoogleSignIn_ToALockedAccountByEmail_IsRefused_AndLinksNothing()
    {
        await using Harness harness = await Harness.CreateAsync(linkGoogle: false);

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.Equal("./Lockout", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Empty(await harness.UserManager.GetLoginsAsync(harness.User));
        Assert.Equal(0, harness.SignInManager.SignInCalls);
    }

    [Fact]
    public async Task GoogleSignIn_ToALockedAccountWithALinkedLogin_IsRefused()
    {
        await using Harness harness = await Harness.CreateAsync(linkGoogle: true);

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.Equal("./Lockout", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(0, harness.SignInManager.SignInCalls);
    }

    /// <summary>A confirmed, locked-out Account and a Google sign-in callback carrying its email.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private Harness(ServiceProvider provider, AsyncServiceScope scope, IdentityUser user,
            UserManager<IdentityUser> userManager, CallbackSignInManager signInManager, ExternalLoginModel page)
        {
            _provider = provider;
            _scope = scope;
            User = user;
            UserManager = userManager;
            SignInManager = signInManager;
            Page = page;
        }

        public IdentityUser User { get; }

        public UserManager<IdentityUser> UserManager { get; }

        public CallbackSignInManager SignInManager { get; }

        public ExternalLoginModel Page { get; }

        public static async Task<Harness> CreateAsync(bool linkGoogle)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IWebHostEnvironment, TestWebHostEnvironment>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"ExternalLoginLockout_{Guid.NewGuid():N}"));
            services.AddDefaultIdentity<IdentityUser>().AddEntityFrameworkStores<AppDbContext>();
            ServiceProvider provider = services.BuildServiceProvider();
            AsyncServiceScope scope = provider.CreateAsyncScope();
            IServiceProvider sp = scope.ServiceProvider;

            UserManager<IdentityUser> userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = Email, Email = Email, EmailConfirmed = true };
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            var info = new ExternalLoginInfo(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Email)], "Google")),
                "Google", "google-key", "Google");
            if (linkGoogle)
            {
                Assert.True((await userManager.AddLoginAsync(user, info)).Succeeded);
            }

            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            var httpContext = new DefaultHttpContext { RequestServices = sp };
            var signInManager = new CallbackSignInManager(info, userManager, httpContext, sp);
            var page = new ExternalLoginModel(signInManager, userManager,
                sp.GetRequiredService<IUserStore<IdentityUser>>(),
                NullLogger<ExternalLoginModel>.Instance, new NoEmailSender(), Options.Create(new BrevoOptions()))
            {
                PageContext = new PageContext { HttpContext = httpContext },
                Url = new ContentUrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
            };
            return new Harness(provider, scope, user, userManager, signInManager, page);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    /// <summary>
    /// Returns a fixed Google sign-in from the callback, and records sign-ins instead of writing a cookie.
    /// </summary>
    private sealed class CallbackSignInManager : SignInManager<IdentityUser>
    {
        private readonly ExternalLoginInfo _info;

        public CallbackSignInManager(ExternalLoginInfo info, UserManager<IdentityUser> userManager,
            HttpContext httpContext, IServiceProvider services)
            : base(userManager, new HttpContextAccessor { HttpContext = httpContext },
                services.GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>(),
                services.GetRequiredService<IOptions<IdentityOptions>>(),
                NullLogger<SignInManager<IdentityUser>>.Instance,
                services.GetRequiredService<IAuthenticationSchemeProvider>(),
                services.GetRequiredService<IUserConfirmation<IdentityUser>>())
        {
            _info = info;
            Context = httpContext;
        }

        public int SignInCalls { get; private set; }

        public override Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(string? expectedXsrf = null) =>
            Task.FromResult<ExternalLoginInfo?>(_info);

        public override Task SignInWithClaimsAsync(IdentityUser user,
            AuthenticationProperties? authenticationProperties,
            IEnumerable<Claim> additionalClaims)
        {
            SignInCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    }

    private sealed class ContentUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => contentPath == "~/" ? "/" : contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public string ApplicationName { get; set; } = "PersonalFinance.Web";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = "Development";
    }
}
