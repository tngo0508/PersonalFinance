using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
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
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.AccountDeletion;
using PersonalFinance.Web.Areas.Identity.Pages.Account;
using PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Services;
using Refit;

namespace PersonalFinance.Tests.AccountDeletion;

/// <summary>
/// The Web side of a Deletion Request: the Delete your account page confirms with the password (or the typed word
/// for Accounts without one) before calling ApiService, emails the cancel link and signs out; the cancel page only
/// cancels on POST.
/// </summary>
public class DeletionPagesTests
{
    private const string Password = "Passw0rd!x";
    private static readonly DateTime PurgeAfter = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(typeof(CancelDeletionModel))]
    [InlineData(typeof(DeletionRequestedModel))]
    [InlineData(typeof(LockoutModel))]
    public void SignedOutPages_AllowAnonymousVisitors(Type pageModel)
    {
        Assert.NotNull(pageModel.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData("DELETE", true)]
    [InlineData("  DELETE ", true)]
    [InlineData("delete", false)]
    [InlineData("DELET", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsConfirmationWord_IsExactAndCaseSensitive(string? typed, bool expected)
    {
        Assert.Equal(expected, DeletePersonalDataModel.IsConfirmationWord(typed));
    }

    [Fact]
    public async Task Delete_WithAWrongPassword_ChangesNothing()
    {
        await using Harness harness = await Harness.CreateAsync(withPassword: true);
        harness.Page.Input.Password = "wrong";

        IActionResult result = await harness.Page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains("Incorrect password.", Errors(harness.Page));
        Assert.Equal(0, harness.Api.RequestCalls);
        Assert.Empty(harness.Email.Sent);
    }

    [Fact]
    public async Task Delete_WithThePassword_RequestsDeletion_EmailsTheCancelLink_AndRedirects()
    {
        await using Harness harness = await Harness.CreateAsync(withPassword: true);
        harness.Page.Input.Password = Password;

        IActionResult result = await harness.Page.OnPostAsync();

        Assert.Equal(1, harness.Api.RequestCalls);
        (string? to, string? subject, string? body) = Assert.Single(harness.Email.Sent);
        Assert.Equal("leaving@example.com", to);
        Assert.Equal("Your account is scheduled for deletion", subject);
        Assert.Contains("https://app.test/Identity/Account/CancelDeletion?token=cancel-token-123", body);
        Assert.Contains("November 21, 2026", body);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/DeletionRequested", redirect.PageName);
        Assert.Equal("2026-11-21", redirect.RouteValues!["purgeAfter"]);
        Assert.Equal(true, redirect.RouteValues["emailSent"]);
    }

    [Theory]
    [InlineData("delete", false)]
    [InlineData("DELETE", true)]
    public async Task Delete_AnAccountWithoutAPassword_ConfirmsByTypingTheWord(string typed, bool accepted)
    {
        await using Harness harness = await Harness.CreateAsync(withPassword: false);
        harness.Page.Input.Confirmation = typed;

        IActionResult result = await harness.Page.OnPostAsync();

        Assert.Equal(accepted ? 1 : 0, harness.Api.RequestCalls);
        if (accepted)
        {
            Assert.IsType<RedirectToPageResult>(result);
        }
        else
        {
            Assert.Contains("Type DELETE to confirm.", Errors(harness.Page));
        }
    }

    [Fact]
    public async Task Delete_TheLastAdmin_ShowsWhy_AndSendsNoEmail()
    {
        await using Harness harness = await Harness.CreateAsync(withPassword: true,
            failure: await ApiExceptionAsync(HttpStatusCode.Conflict, AccountDeletionMessages.LastAdmin));
        harness.Page.Input.Password = Password;

        IActionResult result = await harness.Page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(AccountDeletionMessages.LastAdmin, Errors(harness.Page));
        Assert.Empty(harness.Email.Sent);
    }

    [Fact]
    public async Task CancelLink_Opened_ChangesNothing()
    {
        var api = new FakeAccountDeletionApi();
        var page = new CancelDeletionModel(api, NullLogger<CancelDeletionModel>.Instance)
        { Token = "cancel-token-123" };

        page.OnGet();

        Assert.Equal(0, api.CancelCalls);
        Assert.Null(page.ErrorMessage);
        Assert.False(page.Cancelled);
    }

    [Fact]
    public async Task CancelButton_CancelsWithTheToken()
    {
        var api = new FakeAccountDeletionApi();
        CancelDeletionModel page = CancelPage(api, "cancel-token-123");

        await page.OnPostAsync();

        Assert.True(page.Cancelled);
        Assert.Equal("cancel-token-123", api.LastCancelToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task CancelButton_WithAnInvalidToken_SaysTheLinkIsNotValid(HttpStatusCode status)
    {
        CancelDeletionModel page = CancelPage(new FakeAccountDeletionApi { CancelFailure = await ApiExceptionAsync(status, "x") },
            "used-token");

        await page.OnPostAsync();

        Assert.False(page.Cancelled);
        Assert.Equal(AccountDeletionMessages.InvalidCancelLink, page.ErrorMessage);
    }

    [Fact]
    public async Task CancelButton_WhenApiServiceIsUnreachable_ShowsTheUnavailableMessage()
    {
        CancelDeletionModel page = CancelPage(new FakeAccountDeletionApi { CancelFailure = new HttpRequestException("refused") },
            "cancel-token-123");

        await page.OnPostAsync();

        Assert.False(page.Cancelled);
        Assert.Equal(JsonApiErrorsFilter.UnavailableMessage, page.ErrorMessage);
    }

    [Theory]
    [InlineData("2026-11-21", true)]
    [InlineData("not-a-date", false)]
    [InlineData(null, false)]
    public void DeletionRequested_ShowsOnlyAWellFormedDate(string? purgeAfter, bool hasDate)
    {
        var page = new DeletionRequestedModel();

        page.OnGet(purgeAfter, emailSent: false);

        Assert.Equal(hasDate, page.PurgeAfter.HasValue);
        Assert.False(page.EmailSent);
    }

    [Fact]
    public void DeletionScheduledEmail_EncodesTheLink_AndNamesTheDate()
    {
        string html = EmailTemplateHelper.BuildDeletionScheduledEmail("https://app.test/cancel?token=<x>&y=1", PurgeAfter);

        Assert.Contains("https://app.test/cancel?token=&lt;x&gt;&amp;y=1", html);
        Assert.Contains("November 21, 2026", html);
        Assert.Contains("Cancel Deletion", html);
        Assert.DoesNotContain("<x>", html);
    }

    private static CancelDeletionModel CancelPage(IAccountDeletionApi api, string token) =>
        new(api, NullLogger<CancelDeletionModel>.Instance)
        {
            Token = token,
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

    private static List<string> Errors(PageModel page) =>
        page.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();

    private static async Task<ApiException> ApiExceptionAsync(HttpStatusCode status, string message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.test/api/account-deletion");
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent($$"""{"error":"{{message}}"}""", Encoding.UTF8, "application/json")
        };
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    /// <summary>A signed-in Account on real Identity services (InMemory), with the Delete your account page.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private Harness(ServiceProvider provider, AsyncServiceScope scope, DeletePersonalDataModel page,
            FakeAccountDeletionApi api, FakeEmailSender email)
        {
            _provider = provider;
            _scope = scope;
            Page = page;
            Api = api;
            Email = email;
        }

        public DeletePersonalDataModel Page { get; }

        public FakeAccountDeletionApi Api { get; }

        public FakeEmailSender Email { get; }

        public static async Task<Harness> CreateAsync(bool withPassword, Exception? failure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IWebHostEnvironment, TestWebHostEnvironment>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"DeletionPages_{Guid.NewGuid():N}"));
            services.AddDefaultIdentity<IdentityUser>().AddEntityFrameworkStores<AppDbContext>();
            ServiceProvider provider = services.BuildServiceProvider();
            AsyncServiceScope scope = provider.CreateAsyncScope();

            UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "leaving@example.com", Email = "leaving@example.com" };
            IdentityResult created = withPassword
                ? await userManager.CreateAsync(user, Password)
                : await userManager.CreateAsync(user);
            Assert.True(created.Succeeded);

            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, user.Id)], IdentityConstants.ApplicationScheme))
            };
            httpContext.Request.Scheme = "https";
            // An AsyncLocal accessor value would not flow out of this async method, so set the context directly.
            SignInManager<IdentityUser> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
            signInManager.Context = httpContext;

            var api = new FakeAccountDeletionApi { RequestFailure = failure };
            var email = new FakeEmailSender();
            var page = new DeletePersonalDataModel(userManager,
                signInManager, api, email,
                NullLogger<DeletePersonalDataModel>.Instance)
            {
                PageContext = new PageContext { HttpContext = httpContext },
                Url = new FakeUrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
            };
            return new Harness(provider, scope, page, api, email);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private sealed class FakeAccountDeletionApi : IAccountDeletionApi
    {
        public Exception? RequestFailure { get; init; }

        public Exception? CancelFailure { get; init; }

        public int RequestCalls { get; private set; }

        public int CancelCalls { get; private set; }

        public string? LastCancelToken { get; private set; }

        public Task<DeletionRequestAcceptedDto> RequestAsync(CancellationToken cancellationToken = default)
        {
            RequestCalls++;
            return RequestFailure is null
                ? Task.FromResult(new DeletionRequestAcceptedDto
                { CancelToken = "cancel-token-123", PurgeAfterUtc = PurgeAfter })
                : Task.FromException<DeletionRequestAcceptedDto>(RequestFailure);
        }

        public Task CancelAsync(CancelDeletionRequest request, CancellationToken cancellationToken = default)
        {
            CancelCalls++;
            LastCancelToken = request.Token;
            return CancelFailure is null ? Task.CompletedTask : Task.FromException(CancelFailure);
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Sent.Add((email, subject, htmlMessage));
            return Task.CompletedTask;
        }
    }

    /// <summary>Builds page links as <c>https://app.test/Identity{page}?token=...</c>.</summary>
    private sealed class FakeUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext)
        {
            var values = new RouteValueDictionary(routeContext.Values);
            return $"{routeContext.Protocol}://app.test/{values["area"]}{values["page"]}?token={values["token"]}";
        }
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
