namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Size limits for Feedback, shared by the database schema, the ApiService validator and the Web form.
/// </summary>
public static class FeedbackLimits
{
    /// <summary>
    /// Maximum length of a Feedback message.
    /// </summary>
    public const int MessageMaxLength = 2000;

    /// <summary>
    /// Maximum length of the page path captured with Feedback.
    /// </summary>
    public const int PageUrlMaxLength = 500;
}
