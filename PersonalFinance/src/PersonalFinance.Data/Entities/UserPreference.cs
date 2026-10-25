namespace PersonalFinance.Data.Entities;

/// <summary>
/// An Account's display preferences (localization spec §2.3): its Language now, its Reporting Currency once slice C1
/// lands. One row per Account; it is the source of truth that the culture cookie mirrors. No row means the Account has
/// not chosen, so the request's culture (cookie, then <c>Accept-Language</c>, then English) applies.
/// </summary>
public class UserPreference : AuditableEntity
{
    /// <summary>The UI Language code, one of the Web app's supported languages (<c>en</c>, <c>vi</c>).</summary>
    public string Language { get; set; } = string.Empty;
}
