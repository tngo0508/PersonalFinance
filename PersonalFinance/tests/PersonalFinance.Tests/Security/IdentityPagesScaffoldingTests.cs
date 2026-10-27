using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Areas.Identity.Pages.Account;

namespace PersonalFinance.Tests.Security;

/// <summary>
/// Every page of the ASP.NET Core Identity UI library is scaffolded into <c>Areas/Identity/Pages</c> (localization
/// spec §2.1, slice L1b), so none is served precompiled from the library any more, and the copies keep the library's
/// security behaviour: which pages are anonymous, and the confirm-account link that is shown only without a real
/// email sender.
/// </summary>
public class IdentityPagesScaffoldingTests
{
    private const string AccountNamespace = "PersonalFinance.Web.Areas.Identity.Pages.Account";
    private const string Email = "someone@example.com";

    /// <summary>The Account and Manage pages of Identity UI 10 (Pages/V5), all of which the project now owns.</summary>
    public static readonly TheoryData<string> LibraryPages =
    [
        "AccessDenied", "ConfirmEmail", "ConfirmEmailChange", "ExternalLogin", "ForgotPassword",
        "ForgotPasswordConfirmation", "Lockout", "Login", "LoginWith2fa", "LoginWithRecoveryCode", "Logout",
        "Register", "RegisterConfirmation", "ResendEmailConfirmation", "ResetPassword", "ResetPasswordConfirmation",
        "Manage/ChangePassword", "Manage/DeletePersonalData", "Manage/Disable2fa", "Manage/DownloadPersonalData",
        "Manage/Email", "Manage/EnableAuthenticator", "Manage/ExternalLogins", "Manage/GenerateRecoveryCodes",
        "Manage/Index", "Manage/PersonalData", "Manage/ResetAuthenticator", "Manage/SetPassword",
        "Manage/ShowRecoveryCodes", "Manage/TwoFactorAuthentication"
    ];

    /// <summary>Signed-out pages the library marks <c>[AllowAnonymous]</c>; Logout needs it to override the area convention.</summary>
    public static readonly TheoryData<string> AnonymousPages =
    [
        "ConfirmEmail", "ConfirmEmailChange", "ExternalLogin", "ForgotPassword", "ForgotPasswordConfirmation",
        "Lockout", "LoginWith2fa", "LoginWithRecoveryCode", "Logout", "RegisterConfirmation",
        "ResendEmailConfirmation", "ResetPassword", "ResetPasswordConfirmation"
    ];

    [Theory]
    [MemberData(nameof(LibraryPages))]
    public void LibraryPage_IsScaffoldedIntoTheProject(string page)
    {
        string pageFile = Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), "Areas", "Identity", "Pages",
            "Account", page.Replace('/', Path.DirectorySeparatorChar) + ".cshtml");

        Assert.True(File.Exists(pageFile), $"{page}.cshtml is not in Areas/Identity/Pages/Account.");
        Assert.NotNull(PageModel(page));
    }

    [Theory]
    [MemberData(nameof(AnonymousPages))]
    public void SignedOutPage_AllowsAnonymousVisitors(string page)
    {
        Assert.NotNull(PageModel(page)!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ManagePages_NeverAllowAnonymousVisitors()
    {
        // The Identity area convention authorizes the Manage folder; [AllowAnonymous] on a page would override it.
        List<string> open = typeof(LoginModel).Assembly.GetTypes()
            .Where(t => t.Namespace == AccountNamespace + ".Manage" && typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(open);
    }

    [Fact]
    public async Task RegisterConfirmation_WithARealEmailSender_NeverShowsTheConfirmLink()
    {
        await using IdentityHarness harness = await IdentityWithAccountAsync();
        var page = new RegisterConfirmationModel(harness.UserManager, new RecordingEmailSender());
        harness.Attach(page);

        IActionResult result = await page.OnGetAsync(Email);

        Assert.IsType<PageResult>(result);
        Assert.False(page.DisplayConfirmAccountLink);
        Assert.Null(page.EmailConfirmationUrl);
    }

    [Fact]
    public async Task RegisterConfirmation_WithoutAnEmailSender_ShowsTheConfirmLink()
    {
        await using IdentityHarness harness = await IdentityWithAccountAsync();
        var page = new RegisterConfirmationModel(harness.UserManager, new NoOpEmailSender());
        harness.Attach(page);

        await page.OnGetAsync(Email);

        Assert.True(page.DisplayConfirmAccountLink);
        Assert.Contains("/Account/ConfirmEmail", page.EmailConfirmationUrl);
    }

    [Fact]
    public async Task ForgotPassword_ForAConfirmedAccount_SendsTheResetLinkThroughIdentitysSender()
    {
        await using IdentityHarness harness = await IdentityWithAccountAsync(emailConfirmed: true);
        var sender = new RecordingIdentityEmailSender();
        var page = new ForgotPasswordModel(harness.UserManager, sender)
        {
            Input = new ForgotPasswordModel.InputModel { Email = Email }
        };
        harness.Attach(page);

        IActionResult result = await page.OnPostAsync();

        Assert.Equal("./ForgotPasswordConfirmation", Assert.IsType<RedirectToPageResult>(result).PageName);
        (string to, string link) = Assert.Single(sender.ResetLinks);
        Assert.Equal(Email, to);
        Assert.Contains("/Account/ResetPassword", link);
    }

    [Theory]
    [InlineData("nobody@example.com", true)]
    [InlineData(Email, false)]
    public async Task ForgotPassword_ForAnUnknownOrUnconfirmedAccount_SendsNothing_AndLooksTheSame(string email,
        bool emailConfirmed)
    {
        await using IdentityHarness harness = await IdentityWithAccountAsync(emailConfirmed);
        var sender = new RecordingIdentityEmailSender();
        var page = new ForgotPasswordModel(harness.UserManager, sender)
        {
            Input = new ForgotPasswordModel.InputModel { Email = email }
        };
        harness.Attach(page);

        IActionResult result = await page.OnPostAsync();

        Assert.Equal("./ForgotPasswordConfirmation", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Empty(sender.ResetLinks);
    }

    private static Type? PageModel(string page)
    {
        // The library names one model without the suffix (ForgotPasswordConfirmation); the copy keeps its names.
        string typeName = $"{AccountNamespace}.{page.Replace('/', '.')}";
        return typeof(LoginModel).Assembly.GetType(typeName + "Model") ?? typeof(LoginModel).Assembly.GetType(typeName);
    }

    /// <summary>Identity on InMemory holding one Account for <see cref="Email"/>.</summary>
    private static async Task<IdentityHarness> IdentityWithAccountAsync(bool emailConfirmed = false)
    {
        var identity = IdentityHarness.Create("IdentityScaffolding");
        IdentityResult created = await identity.UserManager.CreateAsync(
            new IdentityUser { UserName = Email, Email = Email, EmailConfirmed = emailConfirmed }, "Passw0rd!x");
        Assert.True(created.Succeeded);
        return identity;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    }

    private sealed class RecordingIdentityEmailSender : IEmailSender<IdentityUser>
    {
        public List<(string To, string Link)> ResetLinks { get; } = [];

        public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink) =>
            Task.CompletedTask;

        public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
        {
            ResetLinks.Add((email, resetLink));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) => Task.CompletedTask;
    }
}
