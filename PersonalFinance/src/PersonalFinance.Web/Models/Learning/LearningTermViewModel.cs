using PersonalFinance.Shared.DTOs.Learning;

namespace PersonalFinance.Web.Models.Learning;

/// <summary>A Term Entry page.</summary>
public sealed class LearningTermViewModel
{
    /// <summary>The entry in the reader's Language.</summary>
    public required TermEntryDto Entry { get; init; }

    /// <summary>The page's absolute canonical URL, without a Language: Language is a cookie, so only English is indexed.</summary>
    public required string CanonicalUrl { get; init; }
}
