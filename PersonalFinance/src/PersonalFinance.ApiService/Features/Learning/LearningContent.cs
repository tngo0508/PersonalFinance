using PersonalFinance.ApiService.Features.Learning.ContentFiles;
using PersonalFinance.ApiService.Features.Learning.Domain;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// <see cref="ILearningContent"/> over the files of one content folder, merged once per Language when it is built.
/// </summary>
internal sealed class LearningContent : ILearningContent
{
    private readonly Dictionary<string, Catalog> _catalogs = new(StringComparer.OrdinalIgnoreCase);

    public LearningContent(LearningContentFileSet files)
    {
        Dictionary<string, TermEntryDocument> englishTerms = InLanguage(files.Terms, LearningContentFiles.English);
        Dictionary<string, LessonDocument> englishLessons = InLanguage(files.Lessons, LearningContentFiles.English);
        IEnumerable<string> languages = files.Terms.Select(f => f.Language)
            .Concat(files.Lessons.Select(f => f.Language))
            .Append(LearningContentFiles.English)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string language in languages)
        {
            Dictionary<string, TermEntryDocument> terms = InLanguage(files.Terms, language);
            Dictionary<string, LessonDocument> lessons = InLanguage(files.Lessons, language);
            _catalogs[language] = new Catalog(
                englishTerms.Select(e => LearningContentMerge.ToTermEntry(e.Key, e.Value, terms.GetValueOrDefault(e.Key)))
                    .OrderBy(t => t.Term, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.Slug, StringComparer.Ordinal)
                    .ToList(),
                englishLessons.Select(e => LearningContentMerge.ToLesson(e.Key, e.Value, lessons.GetValueOrDefault(e.Key)))
                    .OrderBy(l => l.Day)
                    .ThenBy(l => l.Slug, StringComparer.Ordinal)
                    .ToList());
        }
    }

    /// <summary>Reads and merges the content folder at <paramref name="contentDirectory"/>.</summary>
    public static LearningContent Load(string contentDirectory) => new(LearningContentFiles.Read(contentDirectory));

    public IReadOnlyList<TermEntry> GetTerms(string language) => For(language).Terms;

    public IReadOnlyList<TermEntry> GetTermsInTopic(string topic, string language) =>
        For(language).Terms.Where(t => t.Topic == topic).ToList();

    public TermEntry? FindTerm(string slug, string language) => For(language).TermsBySlug.GetValueOrDefault(slug);

    public IReadOnlyList<Lesson> GetLessons(string language) => For(language).Lessons;

    public Lesson? FindLesson(string slug, string language) => For(language).LessonsBySlug.GetValueOrDefault(slug);

    private Catalog For(string language) =>
        _catalogs.GetValueOrDefault(language.Trim()) ?? _catalogs[LearningContentFiles.English];

    // A slug is its file name, so it is unique within a Language.
    private static Dictionary<string, T> InLanguage<T>(IEnumerable<ContentFile<T>> files, string language) =>
        files.Where(f => string.Equals(f.Language, language, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(f => f.Slug, f => f.Document, StringComparer.Ordinal);

    private sealed class Catalog
    {
        public Catalog(List<TermEntry> terms, List<Lesson> lessons)
        {
            Terms = terms;
            Lessons = lessons;
            TermsBySlug = terms.ToDictionary(t => t.Slug, StringComparer.Ordinal);
            LessonsBySlug = lessons.ToDictionary(l => l.Slug, StringComparer.Ordinal);
        }

        public IReadOnlyList<TermEntry> Terms { get; }
        public IReadOnlyList<Lesson> Lessons { get; }
        public Dictionary<string, TermEntry> TermsBySlug { get; }
        public Dictionary<string, Lesson> LessonsBySlug { get; }
    }
}
