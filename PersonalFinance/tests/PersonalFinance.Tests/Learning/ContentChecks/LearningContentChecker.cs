using System.Text.RegularExpressions;
using PersonalFinance.ApiService.Features.Learning.ContentFiles;
using PersonalFinance.ApiService.Features.Learning.Domain;

namespace PersonalFinance.Tests.Learning.ContentChecks;

/// <summary>A content file that breaks a rule: the file, the field (a JSON path) and what is wrong.</summary>
internal sealed record ContentProblem(string File, string Field, string Message)
{
    public override string ToString() => $"{File}: {Field}: {Message}";
}

/// <summary>
/// The build-time content checks of spec #513 and ADR 0019: the measurable rules a Term Entry or Lesson must meet
/// before it is Published. The owner reviews the facts, the tone and clarity; these checks do the counting.
/// </summary>
internal sealed partial class LearningContentChecker
{
    public const int OneLineMaxWords = 15;
    public const int EntryMinWords = 100;
    public const int EntryMaxWords = 250;
    public const int CountryNoteMaxWords = 60;
    public const int SentenceMaxWords = 24;
    public const int OptionMaxWords = 8;

    private readonly ContentRules _rules;
    private readonly List<ContentProblem> _problems = [];

    private LearningContentChecker(ContentRules rules)
    {
        _rules = rules;
    }

    /// <summary>Every problem in the content folder at <paramref name="contentDirectory"/>, in file order.</summary>
    public static IReadOnlyList<ContentProblem> Check(string contentDirectory, ContentRules rules)
    {
        LearningContentFileSet files = LearningContentFiles.Read(contentDirectory);
        var checker = new LearningContentChecker(rules);
        checker.CheckFiles(files);
        return checker._problems;
    }

    private void CheckFiles(LearningContentFileSet files)
    {
        List<ContentFile<TermEntryDocument>> terms = files.Terms.Where(IsEnglish).ToList();
        List<ContentFile<LessonDocument>> lessons = files.Lessons.Where(IsEnglish).ToList();

        CheckIdentity(files.Terms, f => f.Document.Slug, terms.Select(t => t.Slug));
        CheckIdentity(files.Lessons, f => f.Document.Slug, lessons.Select(l => l.Slug));
        terms.ForEach(CheckTermEntry);
        lessons.ForEach(CheckLesson);
        LearningContentReferences.Check(terms, lessons, Add);
    }

    private static bool IsEnglish<T>(ContentFile<T> file) => file.Language == LearningContentFiles.English;

    // ── Identity ───────────────────────────────────────────────────────────────

    private void CheckIdentity<T>(IEnumerable<ContentFile<T>> files, Func<ContentFile<T>, string?> slugOf,
        IEnumerable<string> englishSlugs)
    {
        HashSet<string> english = englishSlugs.ToHashSet(StringComparer.Ordinal);
        foreach (ContentFile<T> file in files)
        {
            if (!Slug().IsMatch(file.Slug))
            {
                Add(file.Path, "slug", "a slug is lowercase words joined by hyphens");
            }

            string? slug = slugOf(file);
            if (slug is not null && slug != file.Slug)
            {
                Add(file.Path, "slug", $"is \"{slug}\" but the file is named \"{file.Slug}\"");
            }

            if (!IsEnglish(file) && !english.Contains(file.Slug))
            {
                Add(file.Path, "slug", $"has no English file {file.Slug}.json");
            }
        }
    }

    // ── Term Entries ───────────────────────────────────────────────────────────

    private void CheckTermEntry(ContentFile<TermEntryDocument> file)
    {
        TermEntryDocument entry = file.Document;
        string path = file.Path;
        Required(path, "slug", entry.Slug);
        Required(path, "term", entry.Term);
        Required(path, "vietnameseName", entry.VietnameseName);
        Required(path, "oneLine", entry.OneLine);
        Required(path, "whyItMatters", entry.WhyItMatters);
        Required(path, "workedExample", entry.WorkedExample);
        RequiredList(path, "explanation", entry.Explanation);
        if (entry.LastReviewed is null)
        {
            Add(path, "lastReviewed", "is missing");
        }

        if (entry.Topic is null || !LearningTopics.All.Contains(entry.Topic))
        {
            Add(path, "topic", $"must be one of {string.Join(", ", LearningTopics.All)}");
        }

        CheckTermWords(path, entry);
        CheckTermProse(path, entry);
        CheckCountryNote(path, "inTheUs", entry.InTheUs, "US");
        CheckCountryNote(path, "inVietnam", entry.InVietnam, "VN");
        CheckCitations(path, "sources", entry.Sources);
        CheckLinks(path, entry);

        int related = entry.RelatedTerms?.Count ?? 0;
        if (related is < 2 or > 5)
        {
            Add(path, "relatedTerms", $"has {related}; an entry has 2–5 related terms");
        }
    }

    private void CheckTermWords(string path, TermEntryDocument entry)
    {
        int oneLine = ContentText.Words(entry.OneLine);
        if (oneLine > OneLineMaxWords)
        {
            Add(path, "oneLine", $"has {oneLine} words; at most {OneLineMaxWords}");
        }

        int words = TermProse(entry).Sum(p => ContentText.Words(p.Text));
        if (words is < EntryMinWords or > EntryMaxWords)
        {
            Add(path, "(entry)", $"has {words} words; an entry has {EntryMinWords}–{EntryMaxWords}, not counting sources");
        }
    }

    private void CheckTermProse(string path, TermEntryDocument entry)
    {
        foreach ((string field, string text) in TermProse(entry))
        {
            CheckProse(path, field, text);
        }
    }

    // The reading text of an entry, which the word, sentence and plain-English rules apply to.
    private static IEnumerable<(string Field, string Text)> TermProse(TermEntryDocument entry)
    {
        IEnumerable<(string, string?)> fields =
        [
            ("oneLine", entry.OneLine),
            .. (entry.Explanation ?? []).Select((p, i) => ($"explanation[{i}]", (string?)p)),
            ("whyItMatters", entry.WhyItMatters),
            ("workedExample", entry.WorkedExample),
            ("inTheUs.text", entry.InTheUs?.Text),
            ("inVietnam.text", entry.InVietnam?.Text),
        ];
        return fields.Where(f => f.Item2 is not null).Select(f => (f.Item1, f.Item2!));
    }

    private void CheckCountryNote(string path, string field, CountryNoteDocument? note, string country)
    {
        if (note is null)
        {
            return;
        }

        Required(path, $"{field}.text", note.Text);
        int words = ContentText.Words(note.Text);
        if (words > CountryNoteMaxWords)
        {
            Add(path, $"{field}.text", $"has {words} words; a country note has at most {CountryNoteMaxWords}");
        }

        if (note.RulesAsOf is not (>= 2000 and <= 2100))
        {
            Add(path, $"{field}.rulesAsOf", "needs the year the note's rules are from");
        }

        CheckCitations(path, $"{field}.sources", note.Sources);
        bool ownCountry = (note.Sources ?? []).Any(s => s.Url is not null && _rules.SourceOf(s.Url)?.Country == country);
        if (!ownCountry)
        {
            Add(path, $"{field}.sources", $"cites no Approved Source from {country}");
        }
    }

    private void CheckCitations(string path, string field, List<SourceDocument>? sources)
    {
        if (sources is not { Count: > 0 })
        {
            Add(path, field, "needs at least one source");
            return;
        }

        for (int i = 0; i < sources.Count; i++)
        {
            SourceDocument source = sources[i];
            string at = $"{field}[{i}]";
            Required(path, $"{at}.name", source.Name);
            if (source.Accessed is null)
            {
                Add(path, $"{at}.accessed", "is missing");
            }

            ApprovedSource? approved = Approved(path, $"{at}.url", source.Url);
            if (approved is { MayBeCited: false })
            {
                Add(path, $"{at}.url", $"{approved.Name} is {approved.Usage}: link it under furtherReading, never cite it");
            }

            if (approved is { RequiresTaxYear: true } && source.TaxYear is null)
            {
                Add(path, $"{at}.taxYear", $"a {approved.Name} citation gives the tax year");
            }
        }
    }

    private void CheckLinks(string path, TermEntryDocument entry)
    {
        List<LinkDocument> links = entry.FurtherReading ?? [];
        for (int i = 0; i < links.Count; i++)
        {
            Required(path, $"furtherReading[{i}].name", links[i].Name);
            Approved(path, $"furtherReading[{i}].url", links[i].Url);
        }

        if (entry.Video is { } video)
        {
            Required(path, "video.title", video.Title);
            Approved(path, "video.url", video.Url);
        }
    }

    private ApprovedSource? Approved(string path, string field, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Add(path, field, "is missing");
            return null;
        }

        ApprovedSource? approved = _rules.SourceOf(url);
        if (approved is null)
        {
            Add(path, field, $"{url} is not under an Approved Source's URL prefix ({ContentRules.AllowlistFile})");
        }

        return approved;
    }

    // ── Lessons ────────────────────────────────────────────────────────────────

    private void CheckLesson(ContentFile<LessonDocument> file)
    {
        LessonDocument lesson = file.Document;
        string path = file.Path;
        Required(path, "slug", lesson.Slug);
        Required(path, "title", lesson.Title);
        if (lesson.Day is not >= 1)
        {
            Add(path, "day", "needs the Lesson's day, from 1");
        }

        CheckProse(path, "title", lesson.Title);
        CheckSentences(path, "intro", lesson.Intro, 2, 3);
        CheckProse(path, "intro", lesson.Intro);

        int terms = lesson.Terms?.Count ?? 0;
        if (terms is < 2 or > 3)
        {
            Add(path, "terms", $"has {terms}; a Lesson teaches 2–3 Term Entries");
        }

        CheckCheck(path, lesson.Check);
        Required(path, "tryIt.label", lesson.TryIt?.Label);
        if (lesson.TryIt?.Url is not { } url || !url.StartsWith('/') || url.StartsWith("//", StringComparison.Ordinal))
        {
            Add(path, "tryIt.url", "is an app path such as /Budget");
        }

        Required(path, "keepsake.symbol", lesson.Keepsake?.Symbol);
        Required(path, "keepsake.name", lesson.Keepsake?.Name);
    }

    private void CheckCheck(string path, List<CheckQuestionDocument>? check)
    {
        int count = check?.Count ?? 0;
        if (count is < 2 or > 3)
        {
            Add(path, "check", $"has {count} questions; a Check has 2–3");
        }

        for (int i = 0; i < count; i++)
        {
            CheckQuestion(path, $"check[{i}]", check![i]);
        }
    }

    private void CheckQuestion(string path, string at, CheckQuestionDocument question)
    {
        int? options = question.Format switch
        {
            CheckFormats.MythOrFact => 2,
            CheckFormats.WhatHappensNext => 3,
            _ => null,
        };
        if (options is null)
        {
            Add(path, $"{at}.format", $"must be {CheckFormats.MythOrFact} or {CheckFormats.WhatHappensNext}");
        }
        else if ((question.Options?.Count ?? 0) != options)
        {
            Add(path, $"{at}.options", $"a {question.Format} question has {options} options");
        }

        List<string> texts = question.Options ?? [];
        for (int i = 0; i < texts.Count; i++)
        {
            int words = ContentText.Words(texts[i]);
            if (words is 0 or > OptionMaxWords)
            {
                Add(path, $"{at}.options[{i}]", $"has {words} words; an option has 1–{OptionMaxWords}");
            }

            CheckProse(path, $"{at}.options[{i}]", texts[i]);
        }

        if (question.CorrectOption is not { } correct || correct < 0 || correct >= texts.Count)
        {
            Add(path, $"{at}.correctOption", "must be the index of one of the options");
        }

        CheckSentences(path, $"{at}.prompt", question.Prompt, 1, 2);
        CheckProse(path, $"{at}.prompt", question.Prompt);
        CheckSentences(path, $"{at}.explanation", question.Explanation, 1, 2);
        CheckProse(path, $"{at}.explanation", question.Explanation);
    }

    // ── Shared rules ───────────────────────────────────────────────────────────

    // No sentence of 25 words or more, and no advice phrasing or listed idiom.
    private void CheckProse(string path, string field, string? text)
    {
        if (text is null)
        {
            return;
        }

        foreach (string sentence in ContentText.Sentences(text))
        {
            int words = ContentText.Words(sentence);
            if (words > SentenceMaxWords)
            {
                Add(path, field, $"has a sentence of {words} words; keep each under 25: \"{sentence}\"");
            }
        }

        foreach (string banned in _rules.BannedPhrasesIn(text))
        {
            Add(path, field, $"uses {banned}");
        }
    }

    private void CheckSentences(string path, string field, string? text, int min, int max)
    {
        int sentences = ContentText.Sentences(text).Count;
        if (sentences < min || sentences > max)
        {
            Add(path, field, $"has {sentences} sentences; {min}–{max}");
        }
    }

    private void Required(string path, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(path, field, "is missing");
        }
    }

    private void RequiredList(string path, string field, List<string>? values)
    {
        if (values is not { Count: > 0 } || values.Any(string.IsNullOrWhiteSpace))
        {
            Add(path, field, "is missing or has an empty item");
        }
    }

    private void Add(string file, string field, string message) => _problems.Add(new ContentProblem(file, field, message));

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Slug();
}
