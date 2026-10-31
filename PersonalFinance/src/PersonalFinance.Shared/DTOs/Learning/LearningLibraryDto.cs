namespace PersonalFinance.Shared.DTOs.Learning;

/// <summary>
/// The Library (GLOSSARY.md) in one Language: every Topic in shelf order, each with its Term Entries by Term. A Topic
/// with no entries yet is listed with none.
/// </summary>
public sealed class LearningLibraryDto
{
    /// <summary>The six Topics, in shelf order.</summary>
    public List<LearningTopicDto> Topics { get; set; } = [];
}

/// <summary>One Topic's shelf.</summary>
public sealed class LearningTopicDto
{
    /// <summary>The Topic's slug (<c>borrowing-credit</c>); the Web names it through an i18n key.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The Topic's Term Entries, by Term.</summary>
    public List<TermSummaryDto> Terms { get; set; } = [];
}

/// <summary>What a book spine and the Library filter need of a Term Entry.</summary>
public sealed class TermSummaryDto
{
    /// <summary>The permanent slug (<c>apr</c>).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The Term in the Language asked for.</summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>
    /// The Term in English, which the filter matches whatever the Language; a shelf in another Language shows it under
    /// the Term.
    /// </summary>
    public string EnglishTerm { get; set; } = string.Empty;

    /// <summary>The Term's Vietnamese name, which the filter matches; never shown.</summary>
    public string VietnameseName { get; set; } = string.Empty;

    /// <summary>Abbreviations and synonyms ("APR").</summary>
    public List<string> AlsoKnownAs { get; set; } = [];

    /// <summary>In one line.</summary>
    public string OneLine { get; set; } = string.Empty;
}
