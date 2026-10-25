using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Services;

/// <summary>
/// The Account emails (confirm address, confirm a new address, reset password, deletion scheduled), each composed in
/// its recipient's stored Language (localization spec §2.3) and sent through the configured <see cref="IEmailSender"/>.
/// An Account with no stored Language gets the current request's Language.
/// </summary>
public interface IAccountEmailService
{
    /// <summary>Asks <paramref name="user"/> to confirm <paramref name="email"/> after registering.</summary>
    Task SendConfirmationAsync(IdentityUser user, string email, string confirmationUrl);

    /// <summary>Asks <paramref name="user"/> to confirm <paramref name="newEmail"/> before it replaces the current one.</summary>
    Task SendEmailChangeAsync(IdentityUser user, string newEmail, string confirmationUrl);

    /// <summary>Sends <paramref name="user"/> the link to choose a new password.</summary>
    Task SendPasswordResetAsync(IdentityUser user, string email, string resetUrl);

    /// <summary>Tells <paramref name="user"/> when the Purge will happen, with the single-use cancel link.</summary>
    Task SendDeletionScheduledAsync(IdentityUser user, string email, string cancelUrl, DateTime purgeAfterUtc);
}

/// <inheritdoc cref="IAccountEmailService" />
internal sealed class AccountEmailService : IAccountEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly IUserPreferenceService _preferences;
    private readonly IStringLocalizer _localizer;
    private readonly string _appName;

    public AccountEmailService(IEmailSender emailSender, IUserPreferenceService preferences,
        IStringLocalizer localizer, IOptions<BrevoOptions> brevoOptions)
    {
        _emailSender = emailSender;
        _preferences = preferences;
        _localizer = localizer;
        _appName = brevoOptions.Value.SenderName ?? "PersonalFinance";
    }

    public Task SendConfirmationAsync(IdentityUser user, string email, string confirmationUrl) =>
        SendAsync(user, email, () => EmailTemplateHelper.BuildConfirmationEmail(_localizer, confirmationUrl, _appName));

    public Task SendEmailChangeAsync(IdentityUser user, string newEmail, string confirmationUrl) =>
        SendAsync(user, newEmail,
            () => EmailTemplateHelper.BuildEmailChangeEmail(_localizer, confirmationUrl, newEmail, _appName));

    public Task SendPasswordResetAsync(IdentityUser user, string email, string resetUrl) =>
        SendAsync(user, email, () => EmailTemplateHelper.BuildPasswordResetEmail(_localizer, resetUrl, _appName));

    public Task SendDeletionScheduledAsync(IdentityUser user, string email, string cancelUrl, DateTime purgeAfterUtc) =>
        SendAsync(user, email,
            () => EmailTemplateHelper.BuildDeletionScheduledEmail(_localizer, cancelUrl, purgeAfterUtc, _appName));

    private async Task SendAsync(IdentityUser user, string email, Func<EmailMessage> compose)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        AppLanguage language = AppLanguages.Find(await _preferences.GetLanguageAsync(user.Id)) ?? AppLanguages.Current();
        EmailMessage message;
        using (LanguageScope.Use(language))
        {
            message = compose();
        }

        await _emailSender.SendEmailAsync(email, message.Subject, message.HtmlBody);
    }
}

/// <summary>
/// The <see cref="IEmailSender{TUser}"/> the Identity pages call (Forgot password), routed to
/// <see cref="IAccountEmailService"/> so those emails are branded and in the recipient's Language too. Replaces the
/// library's English-only default. Links arrive HTML-encoded, per the interface's contract, and are decoded here
/// because the templates encode them.
/// </summary>
internal sealed class IdentityEmailSender : IEmailSender<IdentityUser>
{
    private readonly IAccountEmailService _accountEmails;
    private readonly IEmailSender _emailSender;
    private readonly IStringLocalizer _localizer;

    public IdentityEmailSender(IAccountEmailService accountEmails, IEmailSender emailSender, IStringLocalizer localizer)
    {
        _accountEmails = accountEmails;
        _emailSender = emailSender;
        _localizer = localizer;
    }

    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink) =>
        _accountEmails.SendConfirmationAsync(user, email, WebUtility.HtmlDecode(confirmationLink));

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) =>
        _accountEmails.SendPasswordResetAsync(user, email, WebUtility.HtmlDecode(resetLink));

    /// <summary>Only the Identity API endpoints send reset codes; this app does not map them.</summary>
    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) =>
        _emailSender.SendEmailAsync(email, _localizer["Email.ResetPassword.Heading"],
            WebUtility.HtmlEncode(_localizer.Named("Email.ResetCode.Body", ("code", resetCode))));
}
