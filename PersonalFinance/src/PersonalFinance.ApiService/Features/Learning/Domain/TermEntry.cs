namespace PersonalFinance.ApiService.Features.Learning.Domain;

/// <summary>
/// A Published Term Entry (GLOSSARY.md) in one Language, from its JSON file in the content folder (ADR 0019). Text
/// fields fall back to English one by one where the Vietnamese file leaves them out.
/// </summary>
public sealed record TermEntry
{
    /// <summary>The permanent slug, also the file name (<c>apr</c>). Renaming changes <see cref="Term"/>, never this.</summary>
    public required string Slug { get; init; }

    /// <summary>The Term, unique across the Library ("Annual percentage rate").</summary>
    public required string Term { get; init; }

    /// <summary>The Term's Vietnamese name, shown under it even on the English page.</summary>
    public required string VietnameseName { get; init; }

    /// <summary>Abbreviations and synonyms ("APR"); each belongs to this entry only.</summary>
    public required IReadOnlyList<string> AlsoKnownAs { get; init; }

    /// <summary>The one Topic, a slug from <see cref="LearningTopics.All"/>.</summary>
    public required string Topic { get; init; }

    /// <summary>In one line: at most 15 words.</summary>
    public required string OneLine { get; init; }

    /// <summary>The explanation, one paragraph per item.</summary>
    public required IReadOnlyList<string> Explanation { get; init; }

    /// <summary>What the Term changes for an ordinary person.</summary>
    public required string WhyItMatters { get; init; }

    /// <summary>A worked example with round numbers in USD.</summary>
    public required string WorkedExample { get; init; }

    /// <summary>The optional In the US note.</summary>
    public CountryNote? InTheUs { get; init; }

    /// <summary>The optional In Vietnam note.</summary>
    public CountryNote? InVietnam { get; init; }

    /// <summary>The slugs of 1–5 related Term Entries.</summary>
    public required IReadOnlyList<string> RelatedTerms { get; init; }

    /// <summary>The cited Approved Sources; at least one.</summary>
    public required IReadOnlyList<ContentSource> Sources { get; init; }

    /// <summary>Link-only sources, never paraphrased.</summary>
    public required IReadOnlyList<ContentLink> FurtherReading { get; init; }

    /// <summary>The optional privacy-enhanced YouTube embed from an owner-picked channel.</summary>
    public ContentVideo? Video { get; init; }

    /// <summary>When the facts were last checked against the sources.</summary>
    public required DateOnly LastReviewed { get; init; }
}

/// <summary>An In the US or In Vietnam note: at most 60 words, with the year its rules are from.</summary>
public sealed record CountryNote
{
    /// <summary>The note.</summary>
    public required string Text { get; init; }

    /// <summary>Rules as of this year (the tax year for Tax notes).</summary>
    public required int RulesAsOf { get; init; }

    /// <summary>The note's citations; at least one is from its own country.</summary>
    public required IReadOnlyList<ContentSource> Sources { get; init; }
}

/// <summary>A cited Approved Source: its name, URL and access date.</summary>
public sealed record ContentSource
{
    /// <summary>The page's name ("CFPB, What is APR?").</summary>
    public required string Name { get; init; }

    /// <summary>The page, under an Approved Source's URL prefix.</summary>
    public required string Url { get; init; }

    /// <summary>When the page was read.</summary>
    public required DateOnly Accessed { get; init; }

    /// <summary>The tax year an IRS page describes.</summary>
    public int? TaxYear { get; init; }
}

/// <summary>A Further reading link.</summary>
public sealed record ContentLink
{
    /// <summary>The page's name.</summary>
    public required string Name { get; init; }

    /// <summary>The page, under an Approved Source's URL prefix.</summary>
    public required string Url { get; init; }
}

/// <summary>A Term Entry's video embed.</summary>
public sealed record ContentVideo
{
    /// <summary>The video's title.</summary>
    public required string Title { get; init; }

    /// <summary>The <c>youtube-nocookie.com/embed/</c> URL.</summary>
    public required string Url { get; init; }
}
