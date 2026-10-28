using PersonalFinance.ApiService.Features.Learning.ContentFiles;
using PersonalFinance.ApiService.Features.Learning.Domain;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// Builds a <see cref="TermEntry"/> or <see cref="Lesson"/> in one Language from its English file and that
/// Language's counterpart: each text field the counterpart fills wins, every other field is the English one. Slugs,
/// Topics, references, URLs and dates always come from the English file.
/// </summary>
internal static class LearningContentMerge
{
    public static TermEntry ToTermEntry(string slug, TermEntryDocument english, TermEntryDocument? translated) => new()
    {
        Slug = slug,
        Term = Text(english.Term, translated?.Term),
        VietnameseName = Text(english.VietnameseName, null),
        AlsoKnownAs = english.AlsoKnownAs ?? [],
        Topic = Text(english.Topic, null),
        OneLine = Text(english.OneLine, translated?.OneLine),
        Explanation = Texts(english.Explanation, translated?.Explanation),
        WhyItMatters = Text(english.WhyItMatters, translated?.WhyItMatters),
        WorkedExample = Text(english.WorkedExample, translated?.WorkedExample),
        InTheUs = Note(english.InTheUs, translated?.InTheUs),
        InVietnam = Note(english.InVietnam, translated?.InVietnam),
        RelatedTerms = english.RelatedTerms ?? [],
        Sources = Sources(english.Sources),
        FurtherReading = (english.FurtherReading ?? [])
            .Select(l => new ContentLink { Name = Text(l.Name, null), Url = Text(l.Url, null) })
            .ToList(),
        Video = english.Video is { } video
            ? new ContentVideo { Title = Text(video.Title, translated?.Video?.Title), Url = Text(video.Url, null) }
            : null,
        LastReviewed = english.LastReviewed ?? default,
    };

    public static Lesson ToLesson(string slug, LessonDocument english, LessonDocument? translated) => new()
    {
        Slug = slug,
        Day = english.Day ?? 0,
        Title = Text(english.Title, translated?.Title),
        Intro = Text(english.Intro, translated?.Intro),
        Terms = english.Terms ?? [],
        Check = (english.Check ?? [])
            .Select((question, i) => Question(question, translated?.Check?.ElementAtOrDefault(i)))
            .ToList(),
        TryIt = new TryItLink
        {
            Label = Text(english.TryIt?.Label, translated?.TryIt?.Label),
            Url = Text(english.TryIt?.Url, null),
        },
        Keepsake = new Keepsake
        {
            Symbol = Text(english.Keepsake?.Symbol, null),
            Name = Text(english.Keepsake?.Name, translated?.Keepsake?.Name),
        },
    };

    private static CheckQuestion Question(CheckQuestionDocument english, CheckQuestionDocument? translated) => new()
    {
        Format = Text(english.Format, null),
        Prompt = Text(english.Prompt, translated?.Prompt),
        // The correct option is an index, so a translation counts only with the same number of options.
        Options = translated?.Options is { } options && options.Count == english.Options?.Count
            ? Texts(english.Options, options)
            : english.Options ?? [],
        CorrectOption = english.CorrectOption ?? 0,
        Explanation = Text(english.Explanation, translated?.Explanation),
    };

    private static CountryNote? Note(CountryNoteDocument? english, CountryNoteDocument? translated) =>
        english is null
            ? null
            : new CountryNote
            {
                Text = Text(english.Text, translated?.Text),
                RulesAsOf = english.RulesAsOf ?? 0,
                Sources = Sources(english.Sources),
            };

    private static List<ContentSource> Sources(List<SourceDocument>? sources) =>
        (sources ?? []).Select(s => new ContentSource
        {
            Name = Text(s.Name, null),
            Url = Text(s.Url, null),
            Accessed = s.Accessed ?? default,
            TaxYear = s.TaxYear,
        }).ToList();

    private static string Text(string? english, string? translated) =>
        !string.IsNullOrWhiteSpace(translated) ? translated : english ?? string.Empty;

    // A list of paragraphs or options is one field: a translation replaces it whole, or each blank item falls back.
    private static List<string> Texts(List<string>? english, List<string>? translated)
    {
        if (translated is not { Count: > 0 })
        {
            return english ?? [];
        }

        return translated.Select((text, i) => Text(english?.ElementAtOrDefault(i), text)).ToList();
    }
}
