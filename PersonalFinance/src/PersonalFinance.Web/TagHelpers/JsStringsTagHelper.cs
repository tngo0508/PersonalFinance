using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.TagHelpers;

/// <summary>
/// <c>&lt;js-strings prefix="SavingsGoals.Js." /&gt;</c> hands a page's JavaScript its strings (spec §2.1): a
/// <c>&lt;script type="application/json" id="i18n-strings"&gt;</c> island with every key under the prefix (several may
/// be given, comma-separated), already resolved through the fallback chain. <c>wwwroot/js/core/i18n.js</c> reads it.
/// </summary>
[HtmlTargetElement("js-strings", TagStructure = TagStructure.WithoutEndTag)]
public sealed class JsStringsTagHelper : TagHelper
{
    /// <summary>The id <c>core/i18n.js</c> looks for.</summary>
    public const string ElementId = "i18n-strings";

    // Keeps Vietnamese readable; the encoder still escapes <, > and &, so the JSON cannot close the script element.
    private static readonly JsonSerializerOptions JsonOptions =
        new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    private readonly IStringLocalizer _localizer;

    public JsStringsTagHelper(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>The key prefix, e.g. <c>SavingsGoals.Js.</c>; several prefixes may be separated by commas.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string[] prefixes = Prefix.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        SortedDictionary<string, string> strings = new(StringComparer.Ordinal);
        if (prefixes.Length > 0)
        {
            foreach (LocalizedString entry in _localizer.GetAllStrings(includeParentCultures: true))
            {
                if (prefixes.Any(prefix => entry.Name.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    strings[entry.Name] = entry.Value;
                }
            }
        }

        output.TagName = "script";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "application/json");
        output.Attributes.SetAttribute("id", ElementId);
        output.Content.SetHtmlContent(JsonSerializer.Serialize(strings, JsonOptions));
    }
}
