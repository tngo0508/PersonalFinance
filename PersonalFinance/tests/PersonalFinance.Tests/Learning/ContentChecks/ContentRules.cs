using System.Text.Json;
using System.Text.RegularExpressions;

namespace PersonalFinance.Tests.Learning.ContentChecks;

/// <summary>A row of <c>approved-sources.json</c>: an Approved Source (GLOSSARY.md) and its usage rule.</summary>
internal sealed record ApprovedSource(
    string Name,
    string? Country,
    string Usage,
    IReadOnlyList<string> UrlPrefixes,
    bool RequiresTaxYear = false,
    string? Note = null)
{
    public const string DraftCite = "draft-cite";
    public const string ConceptCiteLink = "concept-cite-link";
    public const string LinkOnly = "link-only";

    public static readonly IReadOnlyList<string> Usages = [DraftCite, ConceptCiteLink, LinkOnly];

    /// <summary>Draft + cite and concept + cite + link sources may be cited; link-only ones only linked.</summary>
    public bool MayBeCited => Usage is DraftCite or ConceptCiteLink;
}

/// <summary>
/// What the content checks measure against, read from the content folder: the Approved Sources allowlist and the
/// plain-English lists (advice phrasing and idioms).
/// </summary>
internal sealed record ContentRules(
    IReadOnlyList<ApprovedSource> Sources,
    IReadOnlyList<string> AdvicePhrases,
    IReadOnlyList<string> Idioms)
{
    public const string AllowlistFile = "approved-sources.json";
    public const string PlainEnglishFile = "plain-english.json";

    public static ContentRules Read(string contentDirectory)
    {
        Allowlist allowlist = ReadJson<Allowlist>(Path.Combine(contentDirectory, AllowlistFile));
        PlainEnglish plainEnglish = ReadJson<PlainEnglish>(Path.Combine(contentDirectory, PlainEnglishFile));
        return new ContentRules(allowlist.Sources, plainEnglish.AdvicePhrases, plainEnglish.Idioms);
    }

    /// <summary>The Approved Source whose URL prefix <paramref name="url"/> starts with, or <c>null</c>.</summary>
    public ApprovedSource? SourceOf(string url) =>
        Sources.FirstOrDefault(s => s.UrlPrefixes.Any(p => url.StartsWith(p, StringComparison.Ordinal)));

    /// <summary>The listed advice phrases and idioms in <paramref name="text"/>, matched as whole words.</summary>
    public IEnumerable<string> BannedPhrasesIn(string text)
    {
        string plain = text.Replace('’', '\'');
        return AdvicePhrases.Where(p => ContainsWords(plain, p)).Select(p => $"advice phrasing \"{p}\"")
            .Concat(Idioms.Where(p => ContainsWords(plain, p)).Select(p => $"idiom \"{p}\""));
    }

    private static bool ContainsWords(string text, string phrase) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(phrase)}\b", RegexOptions.IgnoreCase);

    private static T ReadJson<T>(string file) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(file), JsonSerializerOptions.Web)
        ?? throw new InvalidDataException($"{Path.GetFileName(file)} is empty.");

    private sealed record Allowlist(IReadOnlyList<ApprovedSource> Sources);

    private sealed record PlainEnglish(IReadOnlyList<string> AdvicePhrases, IReadOnlyList<string> Idioms);
}
