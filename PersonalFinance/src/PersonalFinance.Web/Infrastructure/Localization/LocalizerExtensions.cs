using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// PROTOTYPE (#340, throwaway). Named placeholders and the <c>Key.one</c> / <c>Key.other</c> plural convention on top of
/// <see cref="IStringLocalizer"/>, which only models positional arguments.
/// </summary>
public static class LocalizerExtensions
{
    /// <summary><c>L.Named("SavingsGoals.Card.Remaining", ("amount", "$1,200"))</c>.</summary>
    public static string Named(this IStringLocalizer localizer, string key, params (string Name, object? Value)[] args) =>
        JsonStringLocalizer.FormatNamed(localizer[key].Value, args.ToDictionary(a => a.Name, a => a.Value));

    /// <summary>
    /// Named placeholders whose values are markup (e.g. a styled amount span). Literal text from the translation is
    /// HTML-encoded and wrapped in <paramref name="textClass"/>; the arguments are inserted as given.
    /// </summary>
    public static IHtmlContent NamedHtml(this IStringLocalizer localizer, string key, string textClass,
        params (string Name, IHtmlContent Value)[] args)
    {
        Dictionary<string, IHtmlContent> named = args.ToDictionary(a => a.Name, a => a.Value);
        var builder = new HtmlContentBuilder();
        foreach ((bool isPlaceholder, string text) in JsonStringLocalizer.Tokenize(localizer[key].Value))
        {
            if (isPlaceholder && named.TryGetValue(text, out IHtmlContent? value))
            {
                builder.AppendHtml(value);
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendHtml($"<span class=\"{textClass}\">");
                builder.Append(text.Trim());
                builder.AppendHtml("</span>");
            }
        }

        return builder;
    }

    /// <summary>
    /// Picks <c>{key}.{category}</c> for <paramref name="count"/>, falling back to <c>{key}.other</c>; <c>{count}</c> is
    /// always available as a named placeholder.
    /// </summary>
    public static string Plural(this IStringLocalizer localizer, string key, int count,
        params (string Name, object? Value)[] args)
    {
        string category = PluralCategory(CultureInfo.CurrentUICulture, count);
        LocalizedString found = localizer[$"{key}.{category}"];
        if (found.ResourceNotFound)
        {
            found = localizer[$"{key}.other"];
        }

        Dictionary<string, object?> named = args.ToDictionary(a => a.Name, a => a.Value);
        named["count"] = count.ToString("N0", CultureInfo.CurrentCulture);
        return JsonStringLocalizer.FormatNamed(found.Value, named);
    }

    /// <summary>
    /// .NET ships no CLDR plural rules, so a hand-kept table per language. English: one/other. Vietnamese: other only.
    /// JS uses <c>Intl.PluralRules</c>, which agrees for these two.
    /// </summary>
    public static string PluralCategory(CultureInfo uiCulture, int count) =>
        uiCulture.TwoLetterISOLanguageName switch
        {
            "vi" => "other",
            _ => count == 1 ? "one" : "other"
        };
}
