using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// An Account's display preferences (localization spec §2.3): its Language and its Reporting Currency. One row per
/// Account; it is the source of truth that the culture cookie mirrors. No row means the Account has
/// not chosen, so the request's culture (cookie, then <c>Accept-Language</c>, then English) applies.
/// </summary>
public class UserPreference : AuditableEntity
{
    /// <summary>The UI Language code, one of the Web app's supported languages (<c>en</c>, <c>vi</c>).</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The ISO 4217 code totals are shown in, one of <see cref="SupportedCurrencies"/> (ADR 0016); <c>USD</c> by
    /// default. Changing it never rewrites a stored amount.
    /// </summary>
    public string ReportingCurrency { get; set; } = SupportedCurrencies.DefaultCode;

    /// <summary>
    /// Whether the Dashboard shows the quiet Atelier card while a Lesson is open (#513); on by default. Its Hide link
    /// clears it, and a switch on the Atelier page sets it again.
    /// </summary>
    public bool ShowJourneyOnDashboard { get; set; } = true;
}
