namespace PersonalFinance.Shared.Constants;

/// <summary>
/// What a Challenge (GLOSSARY.md) accepts, shared by the database schema, the ApiService validator and the Admin form.
/// </summary>
public static class CommunityChallengeLimits
{
    /// <summary>Maximum length of a Challenge's title.</summary>
    public const int TitleMaxLength = 120;

    /// <summary>Maximum length of the habit it asks for.</summary>
    public const int HabitMaxLength = 1000;

    /// <summary>The most days a Challenge can run, its start and end days included.</summary>
    public const int MaxDays = 366;

    /// <summary>The longest time zone id stored with a membership; a longer one is treated as unknown.</summary>
    public const int TimeZoneIdMaxLength = 64;
}
