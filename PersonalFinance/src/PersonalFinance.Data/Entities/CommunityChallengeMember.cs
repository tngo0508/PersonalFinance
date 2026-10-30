namespace PersonalFinance.Data.Entities;

/// <summary>
/// An Account's membership of a Challenge (GLOSSARY.md): one live row per Account and Challenge. Its time zone decides
/// which local day or week is the Account's current period.
/// </summary>
public class CommunityChallengeMember : AuditableEntity
{
    public int ChallengeId { get; set; }

    /// <summary>
    /// The IANA time zone the browser last sent when joining or checking in (<c>Asia/Ho_Chi_Minh</c>), already checked
    /// to be a known zone; <c>UTC</c> when the browser sent none or an unknown one.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Navigation: the Challenge joined.</summary>
    public CommunityChallenge? Challenge { get; set; }
}
