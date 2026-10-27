using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Areas.Identity.Pages.Account;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// The real <see cref="ExternalLoginModel"/> over a real <see cref="UserManager{TUser}"/> on InMemory, receiving a
/// Google sign-in callback for <see cref="Email"/>. The Account the callback meets is described by
/// <see cref="Options"/>; sign-ins are counted instead of writing a cookie. Identity is registered with roles, as
/// <c>Program.cs</c> does, and the default <see cref="AppRoles.User"/> role exists.
/// </summary>
public sealed class ExternalLoginCallbackHarness : IAsyncDisposable
{
    /// <summary>The email Google returns, and the existing Account's email when there is one.</summary>
    public const string Email = "leaving@example.com";

    /// <summary>The Google login the callback carries.</summary>
    public const string ProviderKey = "google-key";

    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    private ExternalLoginCallbackHarness(ServiceProvider provider, AsyncServiceScope scope, IdentityUser? user,
        UserManager<IdentityUser> userManager, CallbackSignInManager signInManager, ExternalLoginModel page)
    {
        _provider = provider;
        _scope = scope;
        User = user;
        UserManager = userManager;
        SignInManager = signInManager;
        Page = page;
    }

    /// <summary>The Account that already exists for <see cref="Email"/>, or null when none does.</summary>
    public IdentityUser? User { get; }

    public UserManager<IdentityUser> UserManager { get; }

    public CallbackSignInManager SignInManager { get; }

    public ExternalLoginModel Page { get; }

    /// <summary>The Account the callback meets, and what Google sends.</summary>
    public sealed record Options
    {
        /// <summary>Whether an Account with <see cref="Email"/> exists before the callback.</summary>
        public bool AccountExists { get; init; } = true;

        public bool EmailConfirmed { get; init; } = true;

        public bool LockedOut { get; init; }

        /// <summary>Whether the existing Account already has this Google login linked.</summary>
        public bool GoogleLinked { get; init; }

        /// <summary>Claims the existing Account already holds.</summary>
        public IReadOnlyList<Claim> StoredClaims { get; init; } = [];

        /// <summary>Claims Google sends besides the email.</summary>
        public IReadOnlyList<Claim> GoogleClaims { get; init; } = [];
    }

    public static async Task<ExternalLoginCallbackHarness> CreateAsync(Options options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment, TestWebHostEnvironment>();
        services.AddDbContext<AppDbContext>(db =>
            db.UseInMemoryDatabase($"ExternalLoginCallback_{Guid.NewGuid():N}"));
        services.AddDefaultIdentity<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        ServiceProvider provider = services.BuildServiceProvider();
        AsyncServiceScope scope = provider.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;

        Assert.True((await sp.GetRequiredService<RoleManager<IdentityRole>>()
            .CreateAsync(new IdentityRole(AppRoles.User))).Succeeded);

        UserManager<IdentityUser> userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var info = new ExternalLoginInfo(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, Email), .. options.GoogleClaims], "Google")),
            "Google", ProviderKey, "Google");

        IdentityUser? user = null;
        if (options.AccountExists)
        {
            user = new IdentityUser { UserName = Email, Email = Email, EmailConfirmed = options.EmailConfirmed };
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            if (options.GoogleLinked)
            {
                Assert.True((await userManager.AddLoginAsync(user, info)).Succeeded);
            }

            if (options.StoredClaims.Count > 0)
            {
                Assert.True((await userManager.AddClaimsAsync(user, options.StoredClaims)).Succeeded);
            }

            if (options.LockedOut)
            {
                await userManager.SetLockoutEnabledAsync(user, true);
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
        }

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var signInManager = new CallbackSignInManager(info, userManager, httpContext, sp);
        var page = new ExternalLoginModel(signInManager, userManager,
            sp.GetRequiredService<IUserStore<IdentityUser>>(),
            NullLogger<ExternalLoginModel>.Instance, new RecordingAccountEmailService(),
            new InMemoryUserPreferenceService(), TestLocalization.Localizer)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            Url = new ContentUrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
        };
        return new ExternalLoginCallbackHarness(provider, scope, user, userManager, signInManager, page);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    /// <summary>
    /// Returns the fixed Google sign-in from the callback, and records sign-ins instead of writing a cookie.
    /// </summary>
    public sealed class CallbackSignInManager : SignInManager<IdentityUser>
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
