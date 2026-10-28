using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalFinance.ApiService.Features.Learning.ContentFiles;

/// <summary>One content file as read: its path inside the content folder, slug, Language and document.</summary>
internal sealed record ContentFile<T>(string Path, string Slug, string Language, T Document);

/// <summary>Every Term Entry and Lesson file of a content folder, in path order.</summary>
internal sealed record LearningContentFileSet(
    IReadOnlyList<ContentFile<TermEntryDocument>> Terms,
    IReadOnlyList<ContentFile<LessonDocument>> Lessons);

/// <summary>
/// Reads the Atelier content folder (ADR 0019): <c>terms/{slug}.json</c> and <c>lessons/{slug}.json</c> in English,
/// each with an optional <c>{slug}.{language}.json</c> counterpart. A missing <c>terms</c> or <c>lessons</c> folder
/// means no content of that kind.
/// </summary>
internal static class LearningContentFiles
{
    public const string TermsFolder = "terms";
    public const string LessonsFolder = "lessons";
    public const string English = "en";

    // Strict, so a misspelt field name fails with the file's path instead of being dropped.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <exception cref="DirectoryNotFoundException">The content folder does not exist.</exception>
    /// <exception cref="InvalidDataException">A file is not valid JSON of its shape; the message names the file.</exception>
    public static LearningContentFileSet Read(string contentDirectory)
    {
        if (!Directory.Exists(contentDirectory))
        {
            throw new DirectoryNotFoundException($"The Atelier content folder {contentDirectory} does not exist.");
        }

        return new LearningContentFileSet(
            ReadFolder<TermEntryDocument>(contentDirectory, TermsFolder),
            ReadFolder<LessonDocument>(contentDirectory, LessonsFolder));
    }

    private static List<ContentFile<T>> ReadFolder<T>(string contentDirectory, string folder) where T : class
    {
        string directory = Path.Combine(contentDirectory, folder);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(file => ReadFile<T>(file, $"{folder}/{Path.GetFileName(file)}"))
            .ToList();
    }

    private static ContentFile<T> ReadFile<T>(string file, string path) where T : class
    {
        string name = Path.GetFileNameWithoutExtension(file);
        int dot = name.LastIndexOf('.');
        string slug = dot < 0 ? name : name[..dot];
        string language = dot < 0 ? English : name[(dot + 1)..];
        try
        {
            T document = JsonSerializer.Deserialize<T>(File.ReadAllText(file), Options)
                ?? throw new InvalidDataException($"{path}: the file is empty (null).");
            return new ContentFile<T>(path, slug, language, document);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }
}
