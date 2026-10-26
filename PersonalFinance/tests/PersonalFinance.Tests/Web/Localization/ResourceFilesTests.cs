using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// The committed <c>Resources/i18n</c> files: they load, translations only carry English keys with the same
/// placeholders, and every key a DataAnnotations attribute names exists in <c>en.json</c>.
/// </summary>
public partial class ResourceFilesTests
{
    [Fact]
    public void EveryLanguageFile_LoadsAndEnglishHasKeys()
    {
        Assert.NotEmpty(TestLocalization.Catalog.Keys("en"));
    }

    [Fact]
    public void Translations_OnlyHoldEnglishKeys_WithTheSamePlaceholders()
    {
        IReadOnlyCollection<string> english = TestLocalization.Catalog.Keys("en");
        foreach (AppLanguage language in AppLanguages.All.Where(l => l.Code != AppLanguages.Default))
        {
            foreach (string key in TestLocalization.Catalog.Keys(language.Code))
            {
                string englishKey = PluralBase(key) is { } stem && !english.Contains(key) ? stem + ".other" : key;
                Assert.True(english.Contains(englishKey), $"{language.Code}.json has {key}, which en.json does not.");
                TestLocalization.Catalog.TryGet("en", englishKey, out string source);
                TestLocalization.Catalog.TryGet(language.Code, key, out string translation);
                Assert.True(Placeholders(source).SetEquals(Placeholders(translation)),
                    $"{language.Code}.json {key} has placeholders {string.Join(",", Placeholders(translation))}, " +
                    $"English has {string.Join(",", Placeholders(source))}.");
            }
        }
    }

    [Fact]
    public void DataAnnotationKeys_ExistInEnglish()
    {
        IReadOnlyCollection<string> english = TestLocalization.Catalog.Keys("en");
        IEnumerable<string> keys = typeof(JsonStringLocalizer).Assembly.GetTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>().Select(a => a.ErrorMessage)
                .Concat(p.GetCustomAttributes<DisplayAttribute>().Select(a => a.Name)))
            .OfType<string>()
            .Where(JsonStringLocalizer.IsKey)
            .Distinct();

        Assert.All(keys, key => Assert.True(english.Contains(key), $"en.json has no {key}."));
    }

    [Fact]
    public void IdentityPageKeys_ExistInEnglish()
    {
        // Every key a scaffolded Identity page or its model names (L["…"], L.Named, a StatusMessage key), so no
        // Account page logs 3402 LocalizationNotFound.
        IReadOnlyCollection<string> english = TestLocalization.Catalog.Keys("en");
        string pages = Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), "Areas", "Identity");
        List<string> missing = Directory.EnumerateFiles(pages, "*.cs*", SearchOption.AllDirectories)
            .SelectMany(file => QuotedKey().Matches(File.ReadAllText(file))
                .Select(m => (File: Path.GetFileName(file), Key: m.Groups[1].Value)))
            .Where(found => !english.Contains(found.Key))
            .Select(found => $"{found.File}: {found.Key}")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "en.json has no " + string.Join(", ", missing));
    }

    private static string? PluralBase(string key)
    {
        int dot = key.LastIndexOf('.');
        return dot > 0 && key[(dot + 1)..] is "zero" or "one" or "two" or "few" or "many" or "other" ? key[..dot] : null;
    }

    private static HashSet<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"\{[A-Za-z0-9]+\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"""((?:Account|Common|Email|Identity|Validation)\.[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)+)""")]
    private static partial Regex QuotedKey();
}
