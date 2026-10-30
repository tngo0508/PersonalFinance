namespace PersonalFinance.Shared.Constants;

/// <summary>
/// What a Comment (GLOSSARY.md) accepts, shared by the database schema, the ApiService and the Web form, and how many
/// the Admin moderation list shows.
/// </summary>
public static class CommunityCommentLimits
{
    /// <summary>Maximum length of a Comment's text.</summary>
    public const int TextMaxLength = 2000;

    /// <summary>The most Comments the Admin moderation list shows at once, newest first.</summary>
    public const int ReviewListSize = 100;
}
