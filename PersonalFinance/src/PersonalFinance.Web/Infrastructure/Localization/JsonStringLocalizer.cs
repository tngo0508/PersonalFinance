using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Looks a key up through <c>CurrentUICulture → parents → en</c> (spec §2.1):
/// <list type="bullet">
/// <item>found before <c>en</c>: a normal hit;</item>
/// <item>found only in <c>en</c>: English, <c>ResourceNotFound = false</c>, logged as event 3401
/// <c>LocalizationFallback</c> (Information);</item>
/// <item>found nowhere: the key itself, <c>ResourceNotFound = true</c>, logged as event 3402
/// <c>LocalizationNotFound</c> (Warning).</item>
/// </list>
/// A string that is not shaped like a key (<c>"The {0} field is required."</c>, still on the attributes of pages a later
/// slice translates) is returned as it is, without the warning, so untranslated English keeps working quietly.
/// </summary>
internal sealed partial class JsonStringLocalizer : IStringLocalizer
{
    private readonly JsonLocalizationCatalog _catalog;
    private readonly ILogger _logger;

    public JsonStringLocalizer(JsonLocalizationCatalog catalog, ILogger logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    /// <inheritdoc />
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

    /// <summary>Every key resolved for the current UI culture; parents and English always fill the gaps.</summary>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _catalog.Resolve(CultureInfo.CurrentUICulture, string.Empty)
            .Select(pair => new LocalizedString(pair.Key, pair.Value));

    /// <summary>True when <paramref name="name"/> looks like a dotted PascalCase key (<c>SavingsGoals.Card.Remaining</c>).</summary>
    public static bool IsKey(string name) => KeyShape().IsMatch(name);

    private LocalizedString Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!IsKey(name))
        {
            return new LocalizedString(name, name, resourceNotFound: true);
        }

        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        IReadOnlyList<string> chain = JsonLocalizationCatalog.Chain(uiCulture);
        foreach (string language in chain)
        {
            if (!_catalog.TryGet(language, name, out string value))
            {
                continue;
            }

            if (language == AppLanguages.Default && uiCulture.TwoLetterISOLanguageName != AppLanguages.Default)
            {
                LogFallback(_logger, name, uiCulture.Name);
            }

            return new LocalizedString(name, value, resourceNotFound: false, searchedLocation: language + ".json");
        }

        LogNotFound(_logger, name, uiCulture.Name);
        return new LocalizedString(name, name, resourceNotFound: true, searchedLocation: string.Join(" -> ", chain));
    }

    [LoggerMessage(EventId = 3401, EventName = "LocalizationFallback", Level = LogLevel.Information,
        Message = "Localization key {Key} has no {UICulture} translation; using English.")]
    private static partial void LogFallback(ILogger logger, string key, string uiCulture);

    [LoggerMessage(EventId = 3402, EventName = "LocalizationNotFound", Level = LogLevel.Warning,
        Message = "Localization key {Key} is in no resource file for {UICulture}; showing the key.")]
    private static partial void LogNotFound(ILogger logger, string key, string uiCulture);

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*(\.[A-Za-z0-9]+)+$")]
    private static partial Regex KeyShape();
}
