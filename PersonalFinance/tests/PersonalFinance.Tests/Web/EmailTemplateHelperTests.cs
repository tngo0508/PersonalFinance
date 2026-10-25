using System.Globalization;
using System.Net;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using Xunit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// The Account emails: branded layout, encoded links and names, and the text of the current UI culture (localization
/// spec §2.3), English unless a test switches it.
/// </summary>
public class EmailTemplateHelperTests
{
    [Fact]
    public void BuildConfirmationEmail_ContainsBrandedCtaButtonAndConfirmationUrl()
    {
        using var culture = new UiCultureScope("en");
        string testUrl = "https://localhost:7100/Identity/Account/ConfirmEmail?userId=123&code=abc%3D%3D";

        EmailMessage email = EmailTemplateHelper.BuildConfirmationEmail(TestLocalization.Localizer, testUrl, "PersonalFinance");

        Assert.Equal("Confirm your PersonalFinance account", email.Subject);
        Assert.Contains("<html lang=\"en\">", email.HtmlBody);
        Assert.Contains("Confirm your email address", email.HtmlBody);
        Assert.Contains("Confirm Email Address", email.HtmlBody);
        Assert.Contains("Thank you for registering with <strong>PersonalFinance</strong>.", email.HtmlBody);
        Assert.Contains("background-color: #0d6efd", email.HtmlBody);
        Assert.Contains("https://localhost:7100/Identity/Account/ConfirmEmail?userId=123&amp;code=abc%3D%3D", email.HtmlBody);
    }

    [Fact]
    public void BuildConfirmationEmail_WhenAppNameIsNull_DefaultsToPersonalFinance()
    {
        using var culture = new UiCultureScope("en");

        EmailMessage email = EmailTemplateHelper.BuildConfirmationEmail(TestLocalization.Localizer,
            "https://example.com/confirm", null);

        Assert.Contains("PersonalFinance", email.HtmlBody);
        Assert.Contains("https://example.com/confirm", email.HtmlBody);
    }

    [Fact]
    public void BuildConfirmationEmail_EscapesHtmlCharactersInParameters()
    {
        using var culture = new UiCultureScope("en");

        EmailMessage email = EmailTemplateHelper.BuildConfirmationEmail(TestLocalization.Localizer,
            "https://example.com/confirm?token=<test>", "<script>alert('xss')</script>App");

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;App", email.HtmlBody);
        Assert.Contains("https://example.com/confirm?token=&lt;test&gt;", email.HtmlBody);
    }

    [Fact]
    public void BuildPasswordResetEmail_LinksToTheResetPage()
    {
        using var culture = new UiCultureScope("en");

        EmailMessage email = EmailTemplateHelper.BuildPasswordResetEmail(TestLocalization.Localizer,
            "https://app.test/Identity/Account/ResetPassword?code=a&b=c", "PersonalFinance");

        Assert.Equal("Reset your PersonalFinance password", email.Subject);
        Assert.Contains("Reset Password", email.HtmlBody);
        Assert.Contains("https://app.test/Identity/Account/ResetPassword?code=a&amp;b=c", email.HtmlBody);
    }

    [Fact]
    public void BuildEmailChangeEmail_NamesTheNewAddress_Encoded()
    {
        using var culture = new UiCultureScope("en");

        EmailMessage email = EmailTemplateHelper.BuildEmailChangeEmail(TestLocalization.Localizer,
            "https://app.test/confirm", "new<b>@example.com", "PersonalFinance");

        Assert.Contains("<strong>new&lt;b&gt;@example.com</strong>", email.HtmlBody);
        Assert.DoesNotContain("new<b>", email.HtmlBody);
    }

    [Theory]
    [InlineData("en", "October 25, 2026")]
    [InlineData("vi", "25 tháng 10, 2026")]
    public void BuildDeletionScheduledEmail_WritesThePurgeDateInTheRecipientsCulture(string language, string expected)
    {
        AppLanguage recipient = AppLanguages.Find(language)!;
        EmailMessage email;
        using (LanguageScope.Use(recipient))
        {
            email = EmailTemplateHelper.BuildDeletionScheduledEmail(TestLocalization.Localizer,
                "https://app.test/cancel", new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc), "PersonalFinance");
        }

        Assert.Contains($"<strong>{expected}</strong>", WebUtility.HtmlDecode(email.HtmlBody));
    }

    [Fact]
    public void LanguageScope_SetsTheFormattingCulture_AndRestoresIt()
    {
        CultureInfo before = CultureInfo.CurrentCulture;

        using (LanguageScope.Use(AppLanguages.Find("vi")!))
        {
            Assert.Equal("vi-VN", CultureInfo.CurrentCulture.Name);
            Assert.Equal("vi", CultureInfo.CurrentUICulture.Name);
        }

        Assert.Equal(before, CultureInfo.CurrentCulture);
    }

    [Fact]
    public void Emails_FollowTheUiCulture()
    {
        using var culture = new UiCultureScope("vi");
        Assert.True(TestLocalization.Catalog.TryGet("vi", "Email.Confirm.Heading", out string heading));
        Assert.True(TestLocalization.Catalog.TryGet("vi", "Email.Confirm.Button", out string button));

        EmailMessage email = EmailTemplateHelper.BuildConfirmationEmail(TestLocalization.Localizer,
            "https://app.test/confirm", "PersonalFinance");

        Assert.Contains("<html lang=\"vi\">", email.HtmlBody);
        Assert.Contains(heading, email.HtmlBody);
        Assert.Contains(button, email.HtmlBody);
        Assert.DoesNotContain("Confirm your email address", email.HtmlBody);
    }
}
