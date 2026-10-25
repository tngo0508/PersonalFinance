using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Every translation, loaded once at startup from <c>Resources/i18n/&lt;language&gt;.json</c> into one immutable
/// dictionary per language (ADR 0015). Only files named after a supported language are read, so
/// <c>i18n.lock.json</c> and <c>glossary.vi.json</c> beside them are ignored. Malformed JSON fails startup.
/// </summary>
internal sealed class JsonLocalizationCatalog
{
    /// <summary>The resource folder, relative to the content root.</summary>
    public const string ResourcesFolder = "Resources/i18n";

    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _languages;

    /// <summary>Loads the files for <paramref name="languages"/> from <paramref name="resourcesPath"/>.</summary>
    public JsonLocalizationCatalog(string resourcesPath, IEnumerable<string> languages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcesPath);
        ArgumentNullException.ThrowIfNull(languages);

        _languages = languages.ToFrozenDictionary(
            language => language,
            language => Load(Path.Combine(resourcesPath, language + ".json")),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The lookup chain for a UI culture: the culture, each parent (<c>vi-VN</c> → <c>vi</c>), then
    /// <see cref="AppLanguages.Default"/>.
    /// </summary>
    public static IReadOnlyList<string> Chain(CultureInfo uiCulture)
    {
        ArgumentNullException.ThrowIfNull(uiCulture);

        var chain = new List<string>();
        for (CultureInfo culture = uiCulture; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            chain.Add(culture.Name);
        }

        if (!chain.Contains(AppLanguages.Default, StringComparer.OrdinalIgnoreCase))
        {
            chain.Add(AppLanguages.Default);
        }

        return chain;
    }

    /// <summary>Looks a key up in one language only. False when that language has no file or no such key.</summary>
    public bool TryGet(string language, string key, out string value)
    {
        value = string.Empty;
        return _languages.TryGetValue(language, out FrozenDictionary<string, string>? strings)
               && strings.TryGetValue(key, out value!);
    }

    /// <summary>
    /// Every key starting with <paramref name="prefix"/>, resolved through the chain of <paramref name="uiCulture"/> so
    /// the most specific language wins and English fills the gaps.
    /// </summary>
    public IReadOnlyDictionary<string, string> Resolve(CultureInfo uiCulture, string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyList<string> chain = Chain(uiCulture);
        // From the fallback up, so a more specific language overwrites it.
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (!_languages.TryGetValue(chain[i], out FrozenDictionary<string, string>? strings))
            {
                continue;
            }

            foreach ((string key, string value) in strings)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    resolved[key] = value;
                }
            }
        }

        return resolved;
    }

    /// <summary>The keys of one language, for tests and the startup check; empty when the language is unknown.</summary>
    public IReadOnlyCollection<string> Keys(string language) =>
        _languages.TryGetValue(language, out FrozenDictionary<string, string>? strings)
            ? strings.Keys
            : [];

    private static FrozenDictionary<string, string> Load(string file)
    {
        if (!File.Exists(file))
        {
            return FrozenDictionary<string, string>.Empty;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{file} must hold one JSON object of \"Key\": \"text\" pairs.");
        }

        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"{file}: the value of \"{property.Name}\" must be a string.");
            }

            strings[property.Name] = property.Value.GetString()!;
        }

        return strings.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
