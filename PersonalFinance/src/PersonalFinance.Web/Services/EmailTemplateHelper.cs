using System.Globalization;
using System.Net;

namespace PersonalFinance.Web.Services;

public static class EmailTemplateHelper
{
    public static string BuildConfirmationEmail(string confirmationUrl, string? appName = "PersonalFinance")
    {
        string safeAppName = WebUtility.HtmlEncode(appName ?? "PersonalFinance");
        return BuildActionEmail(safeAppName, confirmationUrl,
            title: $"Confirm your {safeAppName} account",
            heading: "Confirm your email address",
            introHtml: $"Thank you for registering with <strong>{safeAppName}</strong>. Please confirm your email address to activate your account and start managing your finances.",
            buttonText: "Confirm Email Address",
            footerHtml: $"If you did not create an account with {safeAppName}, please disregard this email.");
    }

    /// <summary>
    /// The email sent when a Deletion Request is accepted: when the Purge will happen and a single-use link to
    /// cancel it before then.
    /// </summary>
    public static string BuildDeletionScheduledEmail(string cancelUrl, DateTime purgeAfterUtc,
        string? appName = "PersonalFinance")
    {
        string safeAppName = WebUtility.HtmlEncode(appName ?? "PersonalFinance");
        string purgeDate = WebUtility.HtmlEncode(purgeAfterUtc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture));
        return BuildActionEmail(safeAppName, cancelUrl,
            title: $"Your {safeAppName} account will be deleted",
            heading: "Your account is scheduled for deletion",
            introHtml: $"We received your request to delete your <strong>{safeAppName}</strong> account. Your data is hidden and you cannot sign in. On <strong>{purgeDate}</strong> (UTC) your account and data will be deleted permanently. Changed your mind? Cancel before then to restore everything.",
            buttonText: "Cancel Deletion",
            footerHtml: "This link works once. If you did not ask to delete your account, cancel now and change your password.");
    }

    /// <summary>
    /// The shared layout: a heading, an introduction, one button to <paramref name="url"/> and a footer note. Every
    /// argument except <paramref name="url"/> must already be HTML-safe; the URL is encoded here.
    /// </summary>
    private static string BuildActionEmail(string safeAppName, string url, string title, string heading,
        string introHtml, string buttonText, string footerHtml)
    {
        string safeUrl = WebUtility.HtmlEncode(url);

        return $@"<!DOCTYPE html>
<html lang=""en"">
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
                                If the button above does not work, copy and paste the following link into your web browser:
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
                            &copy; {DateTime.UtcNow.Year} {safeAppName}. All rights reserved.
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
