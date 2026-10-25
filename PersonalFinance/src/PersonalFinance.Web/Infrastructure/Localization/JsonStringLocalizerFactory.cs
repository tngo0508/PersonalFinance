using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// PROTOTYPE (#340, throwaway). Loads every JSON file under the resources folder once at startup into one flat
/// dictionary per culture, keyed by dotted key ("SavingsGoals.Card.Remaining").
/// Two file layouts are accepted so they can be compared:
/// <list type="bullet">
/// <item>per language: <c>{root}/en.json</c>, keys already carry the feature prefix;</item>
/// <item>per feature: <c>{root}/SavingsGoals/en.json</c>, the folder name becomes the key prefix.</item>
/// </list>
/// Nested JSON objects are flattened with "." so flat dotted keys and nested objects load to the same dictionary.
/// The factory ignores the base name MVC passes in: one shared key space for Razor, DataAnnotations and JS.
/// </summary>
public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    /// <summary>The culture every lookup ends at when the requested culture chain has no value.</summary>
    public const string FallbackCulture = "en";

    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _cultures;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<JsonStringLocalizer> _logger;

    public JsonStringLocalizerFactory(IConfiguration configuration, IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor, ILogger<JsonStringLocalizer> logger)
    {
        string relative = configuration["Localization:ResourcesPath"] ?? "Resources/i18n";
        ResourcesPath = Path.Combine(environment.ContentRootPath, relative);
        _cultures = Load(ResourcesPath);
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>The absolute folder the resources were loaded from (shown on the prototype panel).</summary>
    public string ResourcesPath { get; }

    public IStringLocalizer Create(Type resourceSource) => CreateLocalizer();

    public IStringLocalizer Create(string baseName, string location) => CreateLocalizer();

    private JsonStringLocalizer CreateLocalizer() => new(this, _httpContextAccessor, _logger);

    /// <summary>
    /// The lookup chain for a UI culture: the culture, each parent ("vi-VN" -> "vi"), then <see cref="FallbackCulture"/>.
    /// </summary>
    public static IReadOnlyList<string> Chain(CultureInfo uiCulture)
    {
        var chain = new List<string>();
        for (CultureInfo c = uiCulture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            chain.Add(c.Name);
        }

        if (!chain.Contains(FallbackCulture))
        {
            chain.Add(FallbackCulture);
        }

        return chain;
    }

    /// <summary>Tries one culture file. Returns false when that culture has no file or no such key.</summary>
    public bool TryGet(string culture, string key, out string value)
    {
        value = string.Empty;
        return _cultures.TryGetValue(culture, out FrozenDictionary<string, string>? strings)
               && strings.TryGetValue(key, out value!);
    }

    /// <summary>All keys starting with <paramref name="prefix"/>, resolved through the chain (used for JS strings).</summary>
    public Dictionary<string, string> Resolve(CultureInfo uiCulture, string prefix)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyList<string> chain = Chain(uiCulture);
        // Walk from the fallback up so the most specific culture wins.
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (!_cultures.TryGetValue(chain[i], out FrozenDictionary<string, string>? strings))
            {
                continue;
            }

            foreach ((string key, string value) in strings)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    result[key] = value;
                }
            }
        }

        return result;
    }

    /// <summary>Short content hash of a resolved bundle, for a cache-busting bundle URL.</summary>
    public static string Hash(Dictionary<string, string> bundle)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(bundle.OrderBy(p => p.Key, StringComparer.Ordinal));
        return Convert.ToHexStringLower(SHA256.HashData(bytes))[..10];
    }

    /// <summary>Keys present in the fallback culture but missing in <paramref name="culture"/> (the CI report).</summary>
    public IEnumerable<string> MissingKeys(string culture)
    {
        FrozenDictionary<string, string> fallback = _cultures.GetValueOrDefault(FallbackCulture)
                                                    ?? FrozenDictionary<string, string>.Empty;
        FrozenDictionary<string, string> target = _cultures.GetValueOrDefault(culture)
                                                  ?? FrozenDictionary<string, string>.Empty;
        return fallback.Keys.Where(k => !target.ContainsKey(k)).Order(StringComparer.Ordinal);
    }

    public IEnumerable<string> Cultures => _cultures.Keys.Order(StringComparer.Ordinal);

    public int KeyCount(string culture) => _cultures.GetValueOrDefault(culture)?.Count ?? 0;

    private static FrozenDictionary<string, FrozenDictionary<string, string>> Load(string root)
    {
        var cultures = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(root))
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string culture = Path.GetFileNameWithoutExtension(file);
                string folder = Path.GetRelativePath(root, Path.GetDirectoryName(file)!);
                string prefix = folder == "." ? string.Empty : folder.Replace(Path.DirectorySeparatorChar, '.') + ".";

                if (!cultures.TryGetValue(culture, out Dictionary<string, string>? strings))
                {
                    strings = new Dictionary<string, string>(StringComparer.Ordinal);
                    cultures[culture] = strings;
                }

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
                Flatten(document.RootElement, prefix, strings);
            }
        }

        return cultures.ToFrozenDictionary(p => p.Key, p => p.Value.ToFrozenDictionary(StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> into)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string key = prefix + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(property.Value, key + ".", into);
            }
            else
            {
                into[key] = property.Value.GetString() ?? string.Empty;
            }
        }
    }
}
