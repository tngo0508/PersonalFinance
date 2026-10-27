using Microsoft.AspNetCore.Identity;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>An <see cref="IUserPreferenceService"/> over a dictionary, for page and controller tests.</summary>
internal sealed class InMemoryUserPreferenceService : IUserPreferenceService
{
    public Dictionary<string, string> Languages { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> ReportingCurrencies { get; } = new(StringComparer.Ordinal);

    public Task<string> GetReportingCurrencyAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReportingCurrencies.TryGetValue(userId, out string? code) ? code : SupportedCurrencies.DefaultCode);

    public Task SetReportingCurrencyAsync(string userId, string currencyCode, CancellationToken cancellationToken = default)
    {
        ReportingCurrencies[userId] = SupportedCurrencies.Find(currencyCode)?.Code
                                      ?? throw new ArgumentException($"Unsupported currency '{currencyCode}'.", nameof(currencyCode));
        return Task.CompletedTask;
    }

    public Task<string?> GetLanguageAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Languages.TryGetValue(userId, out string? language) ? language : null);

    public Task SetLanguageAsync(string userId, string language, CancellationToken cancellationToken = default)
    {
        Languages[userId] = AppLanguages.Find(language)?.Code
                            ?? throw new ArgumentException($"Unsupported language '{language}'.", nameof(language));
        return Task.CompletedTask;
    }
}

/// <summary>An <see cref="IAccountEmailService"/> that records what would have been sent.</summary>
internal sealed class RecordingAccountEmailService : IAccountEmailService
{
    public List<(string Kind, string Email, string Url)> Sent { get; } = [];

    public Task SendConfirmationAsync(IdentityUser user, string email, string confirmationUrl) =>
        Record("Confirmation", email, confirmationUrl);

    public Task SendEmailChangeAsync(IdentityUser user, string newEmail, string confirmationUrl) =>
        Record("EmailChange", newEmail, confirmationUrl);

    public Task SendPasswordResetAsync(IdentityUser user, string email, string resetUrl) =>
        Record("PasswordReset", email, resetUrl);

    public Task SendDeletionScheduledAsync(IdentityUser user, string email, string cancelUrl, DateTime purgeAfterUtc) =>
        Record("DeletionScheduled", email, cancelUrl);

    private Task Record(string kind, string email, string url)
    {
        Sent.Add((kind, email, url));
        return Task.CompletedTask;
    }
}
