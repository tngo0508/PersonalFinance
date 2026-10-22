using System.Globalization;
using System.Text;

namespace PersonalFinance.Shared.Constants;

/// <summary>
/// User-facing Feedback messages, shared by the ApiService validators and endpoints and the Web form.
/// </summary>
public static class FeedbackMessages
{
    /// <summary>Shown when the Category is not Bug, Idea or Other.</summary>
    public const string UnknownCategory = "Choose a category: Bug, Idea or Other.";

    /// <summary>Shown when the message is empty or only whitespace.</summary>
    public const string MessageRequired = "Message is required.";

    /// <summary>
    /// Format of the too-long message: <c>{1}</c> is <see cref="FeedbackLimits.MessageMaxLength"/>. A constant so
    /// the Web form's <c>StringLength</c> attribute can use it; elsewhere use <see cref="MessageTooLong"/>.
    /// </summary>
    public const string MessageTooLongFormat = "Message must be {1:N0} characters or fewer.";

    private static readonly CompositeFormat MessageTooLongCompositeFormat = CompositeFormat.Parse(MessageTooLongFormat);

    /// <summary>Shown when the message is longer than <see cref="FeedbackLimits.MessageMaxLength"/>.</summary>
    public static readonly string MessageTooLong = string.Format(
        CultureInfo.InvariantCulture, MessageTooLongCompositeFormat, null, FeedbackLimits.MessageMaxLength);

    /// <summary>Shown when the page is longer than <see cref="FeedbackLimits.PageUrlMaxLength"/>.</summary>
    public static readonly string PageTooLong = string.Create(CultureInfo.InvariantCulture,
        $"Page must be {FeedbackLimits.PageUrlMaxLength:N0} characters or fewer.");

    /// <summary>Shown when the page is not a path within the app.</summary>
    public const string PageNotAppRelative = "Page must be a path within the app.";

    /// <summary>Shown when a Feedback Status is not New, Reviewed or Archived.</summary>
    public const string UnknownStatus = "Choose a status: New, Reviewed or Archived.";

    /// <summary>Shown when an Account has sent too much Feedback too quickly.</summary>
    public const string RateLimited = "You've sent a lot of feedback recently. Please try again later.";
}
