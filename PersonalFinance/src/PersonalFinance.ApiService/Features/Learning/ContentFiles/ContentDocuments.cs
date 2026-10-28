namespace PersonalFinance.ApiService.Features.Learning.ContentFiles;

// The JSON shape of the content files. Every member is optional, because a Vietnamese file holds only the fields it
// translates and the content checks report a missing English field by name instead of failing to read the file.

/// <summary>A Term Entry file: <c>terms/{slug}.json</c>, or <c>terms/{slug}.vi.json</c> for its Vietnamese.</summary>
internal sealed class TermEntryDocument
{
    public string? Slug { get; set; }
    public string? Term { get; set; }
    public string? VietnameseName { get; set; }
    public List<string>? AlsoKnownAs { get; set; }
    public string? Topic { get; set; }
    public string? OneLine { get; set; }
    public List<string>? Explanation { get; set; }
    public string? WhyItMatters { get; set; }
    public string? WorkedExample { get; set; }
    public CountryNoteDocument? InTheUs { get; set; }
    public CountryNoteDocument? InVietnam { get; set; }
    public List<string>? RelatedTerms { get; set; }
    public List<SourceDocument>? Sources { get; set; }
    public List<LinkDocument>? FurtherReading { get; set; }
    public VideoDocument? Video { get; set; }
    public DateOnly? LastReviewed { get; set; }
}

internal sealed class CountryNoteDocument
{
    public string? Text { get; set; }
    public int? RulesAsOf { get; set; }
    public List<SourceDocument>? Sources { get; set; }
}

internal sealed class SourceDocument
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public DateOnly? Accessed { get; set; }
    public int? TaxYear { get; set; }
}

internal sealed class LinkDocument
{
    public string? Name { get; set; }
    public string? Url { get; set; }
}

internal sealed class VideoDocument
{
    public string? Title { get; set; }
    public string? Url { get; set; }
}

/// <summary>A Lesson file: <c>lessons/{slug}.json</c>, or <c>lessons/{slug}.vi.json</c> for its Vietnamese.</summary>
internal sealed class LessonDocument
{
    public string? Slug { get; set; }
    public int? Day { get; set; }
    public string? Title { get; set; }
    public string? Intro { get; set; }
    public List<string>? Terms { get; set; }
    public List<CheckQuestionDocument>? Check { get; set; }
    public TryItDocument? TryIt { get; set; }
    public KeepsakeDocument? Keepsake { get; set; }
}

internal sealed class CheckQuestionDocument
{
    public string? Format { get; set; }
    public string? Prompt { get; set; }
    public List<string>? Options { get; set; }
    public int? CorrectOption { get; set; }
    public string? Explanation { get; set; }
}

internal sealed class TryItDocument
{
    public string? Label { get; set; }
    public string? Url { get; set; }
}

internal sealed class KeepsakeDocument
{
    public string? Symbol { get; set; }
    public string? Name { get; set; }
}
