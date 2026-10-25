using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Localization;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>A composed email: its subject line and HTML body.</summary>
public sealed class EmailMessage
{
    public EmailMessage(string subject, string htmlBody)
    {
        Subject = subject;
        HtmlBody = htmlBody;
    }

    /// <summary>The subject line, plain text.</summary>
    public string Subject { get; }

    /// <summary>The HTML body.</summary>
    public string HtmlBody { get; }
}

/// <summary>
/// The app's transactional emails, in the current UI culture (localization spec §2.3: the caller sets the recipient's
/// Language first). Every sentence is an <c>Email.*</c> key; markup inside a sentence (the bold app name, the date)
/// goes in through named placeholders, so the translated text around it stays HTML-encoded.
/// </summary>
public static class EmailTemplateHelper
{
    private const string DefaultAppName = "PersonalFinance";

    // Keeps Vietnamese readable in the HTML; <, >, & and quotes are still encoded.
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>The email asking a new Account to confirm its address.</summary>
    public static EmailMessage BuildConfirmationEmail(IStringLocalizer localizer, string confirmationUrl,
        string? appName = DefaultAppName)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        string name = appName ?? DefaultAppName;
        string subject = localizer.Named("Email.Confirm.Subject", ("appName", name));
        return new EmailMessage(subject, BuildActionEmail(localizer, name, confirmationUrl,
            title: Encode(subject),
            heading: Encode(localizer["Email.Confirm.Heading"]),
            introHtml: Html(localizer, "Email.Confirm.Intro", ("appName", Strong(name))),
            buttonText: Encode(localizer["Email.Confirm.Button"]),
            footerHtml: Html(localizer, "Email.Confirm.Footer", ("appName", Text(name)))));
    }

    /// <summary>The email asking an Account to confirm a new email address before it replaces the old one.</summary>
    public static EmailMessage BuildEmailChangeEmail(IStringLocalizer localizer, string confirmationUrl,
        string newEmail, string? appName = DefaultAppName)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        string name = appName ?? DefaultAppName;
        string subject = localizer.Named("Email.EmailChange.Subject", ("appName", name));
        return new EmailMessage(subject, BuildActionEmail(localizer, name, confirmationUrl,
            title: Encode(subject),
            heading: Encode(localizer["Email.EmailChange.Heading"]),
            introHtml: Html(localizer, "Email.EmailChange.Intro", ("email", Strong(newEmail)), ("appName", Strong(name))),
            buttonText: Encode(localizer["Email.Confirm.Button"]),
            footerHtml: Encode(localizer["Email.EmailChange.Footer"])));
    }

    /// <summary>The email with the link to choose a new password.</summary>
    public static EmailMessage BuildPasswordResetEmail(IStringLocalizer localizer, string resetUrl,
        string? appName = DefaultAppName)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        string name = appName ?? DefaultAppName;
        string subject = localizer.Named("Email.ResetPassword.Subject", ("appName", name));
        return new EmailMessage(subject, BuildActionEmail(localizer, name, resetUrl,
            title: Encode(subject),
            heading: Encode(localizer["Email.ResetPassword.Heading"]),
            introHtml: Html(localizer, "Email.ResetPassword.Intro", ("appName", Strong(name))),
            buttonText: Encode(localizer["Email.ResetPassword.Button"]),
            footerHtml: Encode(localizer["Email.ResetPassword.Footer"])));
    }

    /// <summary>
    /// The email sent when a Deletion Request is accepted: when the Purge will happen and a single-use link to
    /// cancel it before then.
    /// </summary>
    public static EmailMessage BuildDeletionScheduledEmail(IStringLocalizer localizer, string cancelUrl,
        DateTime purgeAfterUtc, string? appName = DefaultAppName)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        string name = appName ?? DefaultAppName;
        string purgeDate = purgeAfterUtc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        return new EmailMessage(localizer["Email.Deletion.Subject"], BuildActionEmail(localizer, name, cancelUrl,
            title: Encode(localizer.Named("Email.Deletion.Title", ("appName", name))),
            heading: Encode(localizer["Email.Deletion.Heading"]),
            introHtml: Html(localizer, "Email.Deletion.Intro", ("appName", Strong(name)), ("date", Strong(purgeDate))),
            buttonText: Encode(localizer["Email.Deletion.Button"]),
            footerHtml: Encode(localizer["Email.Deletion.Footer"])));
    }

    private static string Encode(string text) => Encoder.Encode(text);

    private static HtmlString Text(string text) => new HtmlString(WebUtility.HtmlEncode(text));

    private static HtmlString Strong(string text) => new HtmlString($"<strong>{WebUtility.HtmlEncode(text)}</strong>");

    /// <summary>A translated sentence with HTML-encoded text and the given markup in its named placeholders.</summary>
    private static string Html(IStringLocalizer localizer, string key, params (string Name, IHtmlContent Value)[] args)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        localizer.NamedHtml(key, null, args).WriteTo(writer, Encoder);
        return writer.ToString();
    }

    /// <summary>
    /// The shared layout: a heading, an introduction, one button to <paramref name="url"/> and a footer note. Every
    /// text argument must already be HTML-safe; the app name and the URL are encoded here.
    /// </summary>
    private static string BuildActionEmail(IStringLocalizer localizer, string appName, string url, string title,
        string heading, string introHtml, string buttonText, string footerHtml)
    {
        string safeAppName = WebUtility.HtmlEncode(appName);
        string safeUrl = WebUtility.HtmlEncode(url);
        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        string copyLink = Encode(localizer["Email.Common.CopyLink"]);
        string rights = Encode(localizer["Email.Common.Rights"]);

        return $@"<!DOCTYPE html>
<html lang=""{language}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f4f6f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #333333; line-height: 1.6;"">
    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""table-layout: fixed; background-color: #f4f6f9; padding: 40px 0;"">
        <tr>
            <td align=""center"">
                <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05); margin: 0 20px;"">
                    <!-- Header -->
                    <tr>
                        <td align=""center"" style=""background-color: #0d6efd; padding: 30px 20px; color: #ffffff;"">
                            <h1 style=""margin: 0; font-size: 26px; font-weight: 700; letter-spacing: -0.5px;"">{safeAppName}</h1>
                        </td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style=""padding: 40px 30px 30px 30px;"">
                            <h2 style=""margin-top: 0; margin-bottom: 20px; font-size: 20px; font-weight: 600; color: #111827;"">{heading}</h2>
                            <p style=""margin: 0 0 20px 0; font-size: 16px; color: #4b5563;"">
                                {introHtml}
                            </p>
                            <!-- CTA Button -->
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin: 30px 0;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""{safeUrl}"" target=""_blank"" style=""display: inline-block; background-color: #0d6efd; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600; padding: 14px 32px; border-radius: 6px; box-shadow: 0 2px 4px rgba(13, 110, 253, 0.2);"">
                                            {buttonText}
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin: 25px 0 10px 0; font-size: 14px; color: #6b7280;"">
                                {copyLink}
                            </p>
                            <p style=""margin: 0 0 25px 0; font-size: 13px; word-break: break-all; color: #0d6efd;"">
                                <a href=""{safeUrl}"" style=""color: #0d6efd; text-decoration: underline;"">{safeUrl}</a>
                            </p>
                            <hr style=""border: none; border-top: 1px solid #e5e7eb; margin: 30px 0 20px 0;"" />
                            <p style=""margin: 0; font-size: 13px; color: #9ca3af;"">
                                {footerHtml}
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td align=""center"" style=""background-color: #f9fafb; padding: 20px; font-size: 12px; color: #9ca3af; border-top: 1px solid #f3f4f6;"">
                            &copy; {DateTime.UtcNow.Year} {safeAppName}. {rights}
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
