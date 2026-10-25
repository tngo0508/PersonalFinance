using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// PROTOTYPE (#340, throwaway). Looks a key up through <c>CurrentUICulture -> parents -> en</c>.
/// <list type="bullet">
/// <item>Found in the requested chain before "en": normal hit.</item>
/// <item>Found only in "en": a <b>fallback</b>. <c>ResourceNotFound</c> stays false (the user sees English, not a
/// key), the hit is logged at Information with the <c>LocalizationFallback</c> event and recorded for the panel.</item>
/// <item>Found nowhere: <c>ResourceNotFound = true</c>, the key itself is shown, logged as a warning.</item>
/// </list>
/// The CI missing-key report does not depend on runtime logs: it diffs the files
/// (<see cref="JsonStringLocalizerFactory.MissingKeys"/>).
/// </summary>
public sealed partial class JsonStringLocalizer : IStringLocalizer
{
    /// <summary><c>HttpContext.Items</c> key holding the per-request fallback/miss log shown on the prototype panel.</summary>
    public const string RequestLogKey = "i18n.prototype.log";

    private static readonly EventId FallbackEvent = new(3401, "LocalizationFallback");
    private static readonly EventId NotFoundEvent = new(3402, "LocalizationNotFound");

    private readonly JsonStringLocalizerFactory _factory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    public JsonStringLocalizer(JsonStringLocalizerFactory factory, IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        _factory = factory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public LocalizedString this[string name] => Find(name);

    /// <summary>Positional <c>{0}</c> formatting: the contract DataAnnotations and model binding call.</summary>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            LocalizedString found = Find(name);
            string value = string.Format(CultureInfo.CurrentCulture, found.Value, arguments);
            return new LocalizedString(name, value, found.ResourceNotFound, found.SearchedLocation);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _factory.Resolve(CultureInfo.CurrentUICulture, string.Empty)
            .Select(p => new LocalizedString(p.Key, p.Value));

    /// <summary>Named <c>{amount}</c> formatting, matched by name so translators may reorder placeholders.</summary>
    public static string FormatNamed(string template, IReadOnlyDictionary<string, object?> arguments) =>
        NamedPlaceholder().Replace(template, m =>
            arguments.TryGetValue(m.Groups[1].Value, out object? v)
                ? Convert.ToString(v, CultureInfo.CurrentCulture) ?? string.Empty
                : m.Value);

    /// <summary>Splits a template into literal text and placeholder names, for markup-wrapped arguments.</summary>
    public static IEnumerable<(bool IsPlaceholder, string Text)> Tokenize(string template)
    {
        int last = 0;
        foreach (Match m in NamedPlaceholder().Matches(template))
        {
            if (m.Index > last)
            {
                yield return (false, template[last..m.Index]);
            }

            yield return (true, m.Groups[1].Value);
            last = m.Index + m.Length;
        }

        if (last < template.Length)
        {
            yield return (false, template[last..]);
        }
    }

    private LocalizedString Find(string name)
    {
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        foreach (string culture in JsonStringLocalizerFactory.Chain(uiCulture))
        {
            if (!_factory.TryGet(culture, name, out string value))
            {
                continue;
            }

            if (culture == JsonStringLocalizerFactory.FallbackCulture
                && !uiCulture.Name.StartsWith(JsonStringLocalizerFactory.FallbackCulture, StringComparison.Ordinal))
            {
                _logger.LogInformation(FallbackEvent, "Key {Key} missing for {UICulture}, using {Fallback}", name,
                    uiCulture.Name, culture);
                Record("fallback", name);
            }

            return new LocalizedString(name, value, resourceNotFound: false, searchedLocation: culture + ".json");
        }

        _logger.LogWarning(NotFoundEvent, "Key {Key} not found for {UICulture}", name, uiCulture.Name);
        Record("missing", name);
        return new LocalizedString(name, name, resourceNotFound: true,
            searchedLocation: string.Join(" -> ", JsonStringLocalizerFactory.Chain(uiCulture)));
    }

    private void Record(string kind, string key)
    {
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        if (context.Items[RequestLogKey] is not List<(string Kind, string Key)> log)
        {
            log = [];
            context.Items[RequestLogKey] = log;
        }

        if (!log.Contains((kind, key)))
        {
            log.Add((kind, key));
        }
    }

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}")]
    private static partial Regex NamedPlaceholder();
}
