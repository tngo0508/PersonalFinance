namespace PersonalFinance.Shared.Constants;

/// <summary>
/// What a Poll (GLOSSARY.md) accepts, shared by the database schema, the ApiService validator and the Admin form. A
/// Poll's category is one of the board's (<see cref="CommunityStrategyLimits.Categories"/>).
/// </summary>
public static class CommunityPollLimits
{
    /// <summary>Maximum length of a Poll's question.</summary>
    public const int QuestionMaxLength = 500;

    /// <summary>Maximum length of a Poll's description.</summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>Maximum length of one option's text.</summary>
    public const int OptionTextMaxLength = 200;

    /// <summary>The fewest options a Poll can have.</summary>
    public const int MinOptions = 2;

    /// <summary>The most options a Poll can have.</summary>
    public const int MaxOptions = 6;
}
