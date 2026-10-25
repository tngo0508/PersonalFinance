using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Named placeholders and the <c>Key.one</c> / <c>Key.other</c> plural convention (spec §2.1) on top of
/// <see cref="IStringLocalizer"/>, which only models positional arguments. Placeholders are matched by name, so a
/// translation may put them in any order.
/// </summary>
internal static partial class LocalizerExtensions
{
    /// <summary><c>L.Named("SavingsGoals.Card.Remaining", ("amount", "$1,200"))</c>.</summary>
    public static string Named(this IStringLocalizer localizer, string key, params (string Name, object? Value)[] args) =>
        FormatNamed(localizer[key].Value, ToDictionary(args));

    /// <summary>
    /// Named placeholders whose values are markup (an amount in its own span). The template is split on its
    /// placeholders: literal text is HTML-encoded, wrapped in a span of <paramref name="textClass"/> when one is given,
    /// and each argument is inserted as given, wherever the translation puts it.
    /// </summary>
    public static IHtmlContent NamedHtml(this IStringLocalizer localizer, string key, string? textClass,
        params (string Name, IHtmlContent Value)[] args)
    {
        Dictionary<string, IHtmlContent> named = args.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal);
        var builder = new HtmlContentBuilder();
        foreach ((bool isPlaceholder, string text) in Tokenize(localizer[key].Value))
        {
            if (isPlaceholder && named.TryGetValue(text, out IHtmlContent? value))
            {
                builder.AppendHtml(value);
            }
            else if (isPlaceholder)
            {
                builder.Append("{" + text + "}");
            }
            else if (textClass is null)
            {
                builder.Append(text);
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendHtml("<span class=\"").Append(textClass).AppendHtml("\">");
                builder.Append(text.Trim());
                builder.AppendHtml("</span>");
            }
        }

        return builder;
    }

    /// <summary>
    /// Picks <c>{key}.{category}</c> for <paramref name="count"/> in the current UI culture, falling back to
    /// <c>{key}.other</c>; <c>{count}</c> is always available as a named placeholder.
    /// </summary>
    public static string Plural(this IStringLocalizer localizer, string key, int count,
        params (string Name, object? Value)[] args)
    {
        LocalizedString found = localizer[$"{key}.{PluralCategory(CultureInfo.CurrentUICulture, count)}"];
        if (found.ResourceNotFound)
        {
            found = localizer[$"{key}.other"];
        }

        Dictionary<string, object?> named = ToDictionary(args);
        named["count"] = count.ToString("N0", CultureInfo.CurrentCulture);
        return FormatNamed(found.Value, named);
    }

    /// <summary>
    /// The CLDR plural category of <paramref name="count"/>. .NET ships no plural rules, so this is a table per
    /// language, agreeing with <c>Intl.PluralRules</c> in JS: English has <c>one</c> and <c>other</c>, Vietnamese only
    /// <c>other</c>. A new language adds its row here.
    /// </summary>
    public static string PluralCategory(CultureInfo uiCulture, int count) =>
        uiCulture.TwoLetterISOLanguageName switch
        {
            "vi" => "other",
            _ => count == 1 ? "one" : "other"
        };

    /// <summary>Replaces each <c>{name}</c> found in <paramref name="arguments"/>; unknown placeholders stay as written.</summary>
    public static string FormatNamed(string template, IReadOnlyDictionary<string, object?> arguments) =>
        NamedPlaceholder().Replace(template, match =>
            arguments.TryGetValue(match.Groups[1].Value, out object? value)
                ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
                : match.Value);

    /// <summary>Splits a template into literal text and placeholder names, in order.</summary>
    public static IEnumerable<(bool IsPlaceholder, string Text)> Tokenize(string template)
    {
        int last = 0;
        foreach (Match match in NamedPlaceholder().Matches(template))
        {
            if (match.Index > last)
            {
                yield return (false, template[last..match.Index]);
            }

            yield return (true, match.Groups[1].Value);
            last = match.Index + match.Length;
        }

        if (last < template.Length)
        {
            yield return (false, template[last..]);
        }
    }

    private static Dictionary<string, object?> ToDictionary((string Name, object? Value)[] args) =>
        args.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal);

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}")]
    private static partial Regex NamedPlaceholder();
}
