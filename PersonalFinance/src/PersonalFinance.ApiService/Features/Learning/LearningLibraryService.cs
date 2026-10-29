using PersonalFinance.ApiService.Features.Learning.ContentFiles;
using PersonalFinance.ApiService.Features.Learning.Domain;
using PersonalFinance.Shared.DTOs.Learning;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// <see cref="ILearningLibraryService"/> over <see cref="ILearningContent"/>, with today's date from the injected
/// <see cref="TimeProvider"/> for the overdue country notes.
/// </summary>
internal sealed class LearningLibraryService : ILearningLibraryService
{
    /// <summary>An entry reviewed longer ago than this has overdue country notes.</summary>
    private const int ReviewIntervalMonths = 12;

    private readonly ILearningContent _content;
    private readonly TimeProvider _timeProvider;

    public LearningLibraryService(ILearningContent content, TimeProvider timeProvider)
    {
        _content = content;
        _timeProvider = timeProvider;
    }

    public LearningLibraryDto GetLibrary(string? language)
    {
        string code = LanguageOrEnglish(language);
        return new LearningLibraryDto
        {
            Topics = LearningTopics.All.Select(topic => new LearningTopicDto
            {
                Slug = topic,
                Terms = _content.GetTermsInTopic(topic, code).Select(t => new TermSummaryDto
                {
                    Slug = t.Slug,
                    Term = t.Term,
                    EnglishTerm = EnglishTerm(t.Slug) ?? t.Term,
                    VietnameseName = t.VietnameseName,
                    AlsoKnownAs = t.AlsoKnownAs.ToList(),
                    OneLine = t.OneLine,
                }).ToList(),
            }).ToList(),
        };
    }

    public TermEntryDto? FindTerm(string slug, string? language)
    {
        string code = LanguageOrEnglish(language);
        if (_content.FindTerm(slug, code) is not { } entry)
        {
            return null;
        }

        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        bool reviewOverdue = entry.LastReviewed < today.AddMonths(-ReviewIntervalMonths);
        return new TermEntryDto
        {
            Slug = entry.Slug,
            Term = entry.Term,
            VietnameseName = entry.VietnameseName,
            AlsoKnownAs = entry.AlsoKnownAs.ToList(),
            Topic = entry.Topic,
            OneLine = entry.OneLine,
            Explanation = entry.Explanation.ToList(),
            WhyItMatters = entry.WhyItMatters,
            WorkedExample = entry.WorkedExample,
            InTheUs = Note(entry.InTheUs, entry.Topic, reviewOverdue, today),
            InVietnam = Note(entry.InVietnam, entry.Topic, reviewOverdue, today),
            RelatedTerms = entry.RelatedTerms
                .Select(s => _content.FindTerm(s, code))
                .OfType<TermEntry>()
                .Select(t => new TermLinkDto { Slug = t.Slug, Term = t.Term })
                .ToList(),
            Sources = entry.Sources.Select(Source).ToList(),
            FurtherReading = entry.FurtherReading.Select(l => new ContentLinkDto { Name = l.Name, Url = l.Url })
                .ToList(),
            Video = entry.Video is { } video ? new ContentVideoDto { Title = video.Title, Url = video.Url } : null,
            LastReviewed = entry.LastReviewed,
            Mentions = Mentions(entry.Slug, code),
        };
    }

    // The names of every other entry, longest first so "capital gains tax" wins over "tax".
    private List<TermMentionDto> Mentions(string slug, string language) =>
        _content.GetTerms(language)
            .Where(t => t.Slug != slug)
            .SelectMany(t => new[] { t.Term, EnglishTerm(t.Slug) ?? t.Term }.Concat(t.AlsoKnownAs)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(text => new TermMentionDto { Text = text, Slug = t.Slug }))
            .OrderByDescending(m => m.Text.Length)
            .ThenBy(m => m.Text, StringComparer.Ordinal)
            .ToList();

    private string? EnglishTerm(string slug) => _content.FindTerm(slug, LearningContentFiles.English)?.Term;

    private static CountryNoteDto? Note(CountryNote? note, string topic, bool reviewOverdue, DateOnly today) =>
        note is null
            ? null
            : new CountryNoteDto
            {
                Text = note.Text,
                RulesAsOf = note.RulesAsOf,
                IsOverdue = reviewOverdue || (topic == LearningTopics.Tax && note.RulesAsOf < today.Year),
                Sources = note.Sources.Select(Source).ToList(),
            };

    private static ContentSourceDto Source(ContentSource source) => new()
    {
        Name = source.Name,
        Url = source.Url,
        Accessed = source.Accessed,
        TaxYear = source.TaxYear,
    };

    private static string LanguageOrEnglish(string? language) =>
        string.IsNullOrWhiteSpace(language) ? LearningContentFiles.English : language;
}
