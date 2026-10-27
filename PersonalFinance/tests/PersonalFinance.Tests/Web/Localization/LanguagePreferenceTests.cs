using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// The stored Language (localization spec §2.3): the preference row, the culture cookie written from it at sign-in,
/// emails composed in the recipient's Language, and Notifications rendered from their key at read time.
/// </summary>
public class LanguagePreferenceTests
{
    private const string UserId = "user-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    // --- UserPreferenceService ---

    [Fact]
    public async Task Preference_NeverChosen_ReadsAsNull()
    {
        await using AppDbContext db = CreateContext();

        Assert.Null(await new UserPreferenceService(db, new FixedTimeProvider(Now)).GetLanguageAsync(UserId));
    }

    [Fact]
    public async Task Preference_SetTwice_KeepsOneRowWithTheLatestLanguage()
    {
        await using AppDbContext db = CreateContext();
        var service = new UserPreferenceService(db, new FixedTimeProvider(Now));

        await service.SetLanguageAsync(UserId, "vi");
        await service.SetLanguageAsync(UserId, "EN");

        UserPreference row = Assert.Single(db.UserPreferences.IgnoreQueryFilters());
        Assert.Equal("en", row.Language);
        Assert.Equal(Now.UtcDateTime, row.UpdatedAt);
        Assert.Equal("en", await service.GetLanguageAsync(UserId));
    }

    [Fact]
    public async Task Preference_IsReadForTheAskedAccountOnly_AndIgnoresSoftDeletedRows()
    {
        await using AppDbContext db = CreateContext();
        db.UserPreferences.Add(new UserPreference { UserId = "someone-else", Language = "vi" });
        var deleted = new UserPreference { UserId = UserId, Language = "vi" };
        deleted.SoftDelete();
        db.UserPreferences.Add(deleted);
        await db.SaveChangesAsync();

        Assert.Null(await new UserPreferenceService(db, new FixedTimeProvider(Now)).GetLanguageAsync(UserId));
    }

    [Fact]
    public async Task Preference_UnsupportedLanguage_IsRejected_AndStoresNothing()
    {
        await using AppDbContext db = CreateContext();
        var service = new UserPreferenceService(db, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetLanguageAsync(UserId, "fr"));
        Assert.Empty(db.UserPreferences);
    }

    // --- Culture cookie at sign-in ---

    [Fact]
    public async Task SignIn_WithAStoredLanguage_WritesTheCultureCookie()
    {
        var preferences = new InMemoryUserPreferenceService();
        preferences.Languages[UserId] = "vi";
        HttpContext context = SignInContext(preferences);

        await LanguageSignInEvents.WriteStoredLanguageAsync(context, SignedIn(UserId));

        Assert.Contains(".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi", context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task SignIn_WithoutAStoredLanguage_LeavesTheRequestsLanguage()
    {
        HttpContext context = SignInContext(new InMemoryUserPreferenceService());

        await LanguageSignInEvents.WriteStoredLanguageAsync(context, SignedIn(UserId));

        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task SignIn_WhenThePreferenceCannotBeRead_StillSignsIn_AndLogs()
    {
        var logger = new EventRecordingLogger();
        HttpContext context = SignInContext(new FailingPreferenceService(), logger);

        await LanguageSignInEvents.WriteStoredLanguageAsync(context, SignedIn(UserId));

        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Equal(3403, Assert.Single(logger.Entries).EventId.Id);
    }

    // --- Account emails ---

    [Fact]
    public async Task AccountEmail_UsesTheRecipientsStoredLanguage_NotTheRequests()
    {
        using var culture = new UiCultureScope("en");
        var preferences = new InMemoryUserPreferenceService();
        preferences.Languages[UserId] = "vi";
        var sender = new CapturingEmailSender();
        var emails = new AccountEmailService(sender, preferences, TestLocalization.Localizer,
            Options.Create(new BrevoOptions()));

        await emails.SendPasswordResetAsync(new IdentityUser { Id = UserId }, "me@example.com", "https://app.test/reset");

        TestLocalization.Catalog.TryGet("vi", "Email.ResetPassword.Heading", out string heading);
        (string to, string subject, string body) = Assert.Single(sender.Sent);
        Assert.Equal("me@example.com", to);
        Assert.NotEqual("Reset your PersonalFinance password", subject);
        Assert.Contains("<html lang=\"vi\"", body);
        Assert.Contains(heading, body);
        Assert.Equal("en", System.Globalization.CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public async Task AccountEmail_WithoutAStoredLanguage_UsesTheRequestsLanguage()
    {
        using var culture = new UiCultureScope("en-US");
        var sender = new CapturingEmailSender();
        var emails = new AccountEmailService(sender, new InMemoryUserPreferenceService(), TestLocalization.Localizer,
            Options.Create(new BrevoOptions()));

        await emails.SendConfirmationAsync(new IdentityUser { Id = UserId }, "me@example.com", "https://app.test/c");

        Assert.Equal("Confirm your PersonalFinance account", Assert.Single(sender.Sent).Subject);
    }

    [Fact]
    public async Task IdentityEmailSender_DecodesTheLinkIdentityEncoded_SoItIsEncodedOnce()
    {
        var accountEmails = new RecordingAccountEmailService();
        var sender = new IdentityEmailSender(accountEmails, new CapturingEmailSender(), TestLocalization.Localizer);

        await sender.SendPasswordResetLinkAsync(new IdentityUser { Id = UserId }, "me@example.com",
            "https://app.test/reset?code=a&amp;b=c");

        Assert.Equal(("PasswordReset", "me@example.com", "https://app.test/reset?code=a&b=c"),
            Assert.Single(accountEmails.Sent));
    }

    // --- Notifications rendered at read time ---

    [Fact]
    public void Notification_WithKeys_IsRenderedInTheReadersLanguage()
    {
        using var culture = new UiCultureScope("vi");
        NotificationDto notification = Milestone();

        NotificationText.Localize(TestLocalization.Localizer, notification);

        TestLocalization.Catalog.TryGet("vi", "Notifications.SavingsMilestone.Title", out string title);
        Assert.Equal(title.Replace("{percent}", "50"), notification.Title);
        Assert.Contains("Vacation", notification.Message);
        Assert.Contains("$600.00", notification.Message);
        Assert.DoesNotContain("{", notification.Message);
    }

    [Fact]
    public void Notification_WithKeys_InEnglish_ReadsAsItWasStored()
    {
        using var culture = new UiCultureScope("en");
        NotificationDto notification = Milestone();

        NotificationText.Localize(TestLocalization.Localizer, notification);

        Assert.Equal("50% savings milestone reached!", notification.Title);
        Assert.Equal("Your goal 'Vacation' has reached 50% of its target ($600.00 of $1,000.00).", notification.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Notifications.Gone.Title")]
    public void Notification_WithoutAKnownKey_KeepsItsStoredText(string? key)
    {
        using var culture = new UiCultureScope("vi");
        var notification = new NotificationDto { Title = "Stored title", Message = "Stored message", TitleKey = key };

        NotificationText.Localize(TestLocalization.Localizer, notification);

        Assert.Equal("Stored title", notification.Title);
        Assert.Equal("Stored message", notification.Message);
    }

    // --- Account settings › Language ---

    [Fact]
    public async Task LanguagePage_Get_ShowsTheStoredLanguage()
    {
        var preferences = new InMemoryUserPreferenceService();
        preferences.Languages[UserId] = "vi";
        await using PageHarness harness = PageHarness.Create(preferences);

        await harness.Page.OnGetAsync();

        Assert.Equal("vi", harness.Page.Language);
    }

    [Fact]
    public async Task LanguagePage_Post_StoresTheChoiceForTheSignedInAccount_AndWritesTheCookie()
    {
        var preferences = new InMemoryUserPreferenceService();
        await using PageHarness harness = PageHarness.Create(preferences);
        harness.Page.Language = "vi";

        IActionResult result = await harness.Page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(new Dictionary<string, string> { [UserId] = "vi" }, preferences.Languages);
        Assert.Contains("uic%3Dvi", harness.Context.Response.Headers.SetCookie.ToString());
        Assert.True(harness.Page.Saved);
    }

    [Fact]
    public async Task LanguagePage_PostUnsupported_ShowsAnError_AndStoresNothing()
    {
        var preferences = new InMemoryUserPreferenceService();
        await using PageHarness harness = PageHarness.Create(preferences);
        harness.Page.Language = "fr";

        IActionResult result = await harness.Page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(harness.Page.ModelState.IsValid);
        Assert.Empty(preferences.Languages);
        Assert.Equal(0, harness.Context.Response.Headers.SetCookie.Count);
    }

    private static NotificationDto Milestone() => new()
    {
        Title = "50% savings milestone reached!",
        Message = "Your goal 'Vacation' has reached 50% of its target ($600.00 of $1,000.00).",
        TitleKey = "Notifications.SavingsMilestone.Title",
        MessageKey = "Notifications.SavingsMilestone.Message",
        Arguments = new Dictionary<string, string>
        {
            ["percent"] = "50",
            ["goalName"] = "Vacation",
            ["current"] = "$600.00",
            ["target"] = "$1,000.00"
        }
    };

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LanguagePreference_{Guid.NewGuid():N}")
            .Options);

    private static ClaimsPrincipal SignedIn(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], IdentityConstants.ApplicationScheme));

    private static DefaultHttpContext SignInContext(IUserPreferenceService preferences, ILogger? logger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(preferences);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddSingleton<ILoggerFactory>(new SingleLoggerFactory(logger ?? NullLogger.Instance));
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Scheme = "https";
        return context;
    }

    /// <summary>The Account settings › Language page for a signed-in Account, over a real UserManager.</summary>
    private sealed class PageHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private PageHarness(ServiceProvider provider, PersonalFinance.Web.Areas.Identity.Pages.Account.Manage.LanguageModel page,
            HttpContext context)
        {
            _provider = provider;
            Page = page;
            Context = context;
        }

        public PersonalFinance.Web.Areas.Identity.Pages.Account.Manage.LanguageModel Page { get; }

        public HttpContext Context { get; }

        public static PageHarness Create(IUserPreferenceService preferences)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase($"LanguagePage_{Guid.NewGuid():N}"));
            services.AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<AppDbContext>();
            ServiceProvider provider = services.BuildServiceProvider();

            var context = new DefaultHttpContext { RequestServices = provider, User = SignedIn(UserId) };
            context.Request.Scheme = "https";
            var page = new PersonalFinance.Web.Areas.Identity.Pages.Account.Manage.LanguageModel(
                provider.GetRequiredService<UserManager<IdentityUser>>(), preferences, TestLocalization.Localizer,
                new FixedTimeProvider(Now))
            {
                PageContext = new PageContext { HttpContext = context },
                TempData = new TempDataDictionary(context, new TestTempDataProvider())
            };
            return new PageHarness(provider, page, context);
        }

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }

    private sealed class FailingPreferenceService : IUserPreferenceService
    {
        public Task<string> GetReportingCurrencyAsync(string userId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");

        public Task SetReportingCurrencyAsync(string userId, string currencyCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");

        public Task<string?> GetLanguageAsync(string userId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");

        public Task SetLanguageAsync(string userId, string language, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Sent.Add((email, subject, htmlMessage));
            return Task.CompletedTask;
        }
    }

    private sealed class SingleLoggerFactory : ILoggerFactory
    {
        private readonly ILogger _logger;

        public SingleLoggerFactory(ILogger logger)
        {
            _logger = logger;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => _logger;

        public void Dispose()
        {
        }
    }
}
