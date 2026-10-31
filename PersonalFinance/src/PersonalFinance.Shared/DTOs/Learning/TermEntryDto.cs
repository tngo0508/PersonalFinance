namespace PersonalFinance.Shared.DTOs.Learning;

/// <summary>
/// A Published Term Entry (GLOSSARY.md) in one Language, each text field falling back to English where the Language's
/// file leaves it out.
/// </summary>
public sealed class TermEntryDto
{
    /// <summary>The permanent slug (<c>apr</c>).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The Term.</summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>
    /// The Term in English. A page in another Language shows it under the Term, tying the new word to the one on US
    /// forms; an English page shows nothing there.
    /// </summary>
    public string EnglishTerm { get; set; } = string.Empty;

    /// <summary>Abbreviations and synonyms.</summary>
    public List<string> AlsoKnownAs { get; set; } = [];

    /// <summary>The Topic's slug.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>In one line; also the page's meta description.</summary>
    public string OneLine { get; set; } = string.Empty;

    /// <summary>The explanation, one paragraph per item.</summary>
    public List<string> Explanation { get; set; } = [];

    /// <summary>Why it matters.</summary>
    public string WhyItMatters { get; set; } = string.Empty;

    /// <summary>A worked example.</summary>
    public string WorkedExample { get; set; } = string.Empty;

    /// <summary>The optional In the US note.</summary>
    public CountryNoteDto? InTheUs { get; set; }

    /// <summary>The optional In Vietnam note.</summary>
    public CountryNoteDto? InVietnam { get; set; }

    /// <summary>The related Term Entries, in the entry's order.</summary>
    public List<TermLinkDto> RelatedTerms { get; set; } = [];

    /// <summary>The cited sources.</summary>
    public List<ContentSourceDto> Sources { get; set; } = [];

    /// <summary>Further reading links.</summary>
    public List<ContentLinkDto> FurtherReading { get; set; } = [];

    /// <summary>The optional privacy-enhanced YouTube embed.</summary>
    public ContentVideoDto? Video { get; set; }

    /// <summary>When the facts were last checked against the sources.</summary>
    public DateOnly LastReviewed { get; set; }

    /// <summary>The Lesson that teaches this entry, or null when no Lesson does yet.</summary>
    public LessonSummaryDto? Lesson { get; set; }

    /// <summary>
    /// The words that name another Term Entry (its Term in this Language, its English Term and its aliases), longest
    /// first, so the page can link the ones its text mentions.
    /// </summary>
    public List<TermMentionDto> Mentions { get; set; } = [];
}

/// <summary>An In the US or In Vietnam note.</summary>
public sealed class CountryNoteDto
{
    /// <summary>The note.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Rules as of this year.</summary>
    public int RulesAsOf { get; set; }

    /// <summary>
    /// The rules may have changed: the entry was reviewed more than 12 months ago, or this is a Tax note whose year is
    /// before the current year.
    /// </summary>
    public bool IsOverdue { get; set; }

    /// <summary>The note's citations.</summary>
    public List<ContentSourceDto> Sources { get; set; } = [];
}

/// <summary>A link to another Term Entry.</summary>
public sealed class TermLinkDto
{
    /// <summary>Its slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Its Term in the Language asked for.</summary>
    public string Term { get; set; } = string.Empty;
}

/// <summary>A word or phrase in the text that names another Term Entry.</summary>
public sealed class TermMentionDto
{
    /// <summary>The words, matched whole and ignoring case.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The slug of the entry they name.</summary>
    public string Slug { get; set; } = string.Empty;
}

/// <summary>A cited source.</summary>
public sealed class ContentSourceDto
{
    /// <summary>The page's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The page.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>When the page was read.</summary>
    public DateOnly Accessed { get; set; }

    /// <summary>The tax year an IRS page describes.</summary>
    public int? TaxYear { get; set; }
}

/// <summary>A Further reading link.</summary>
public sealed class ContentLinkDto
{
    /// <summary>The page's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The page.</summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>A Term Entry's video embed.</summary>
public sealed class ContentVideoDto
{
    /// <summary>The video's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The <c>youtube-nocookie.com/embed/</c> URL.</summary>
    public string Url { get; set; } = string.Empty;
}
