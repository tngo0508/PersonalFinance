namespace PersonalFinance.Data.Entities;

/// <summary>
/// One finished Lesson of the Journey (GLOSSARY.md): the moment an Account answered the Lesson's Check. One row per
/// Account and Lesson among non-deleted rows; retaking the Check never adds another. The next Lesson opens at the
/// local midnight after <see cref="FinishedAt"/> in <see cref="TimeZoneId"/>. Check answers are never stored.
/// </summary>
public class LessonCompletion : AuditableEntity
{
    /// <summary>The Lesson's permanent slug (<c>build-a-cushion</c>), a file in the content folder (ADR 0019).</summary>
    public string LessonSlug { get; set; } = string.Empty;

    /// <summary>When the Check was answered (UTC).</summary>
    public DateTime FinishedAt { get; set; }

    /// <summary>
    /// The IANA time zone the browser sent with the finish (<c>Asia/Ho_Chi_Minh</c>), already checked to be a known
    /// zone; <c>UTC</c> when the browser sent none or an unknown one.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";
}
