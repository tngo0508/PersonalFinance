using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.TagHelpers;

/// <summary>
/// <c>&lt;research-term key="volatility"&gt;Volatility&lt;/research-term&gt;</c> marks a Research glossary term: it renders
/// <c>&lt;button type="button" class="research-term" data-term-text="..."&gt;</c> around the text, carrying the term's
/// explanation (<c>Research.Glossary.&lt;Key&gt;</c>) in the request's Language for <c>research-glossary.js</c> to show
/// on click. The key matches a glossary entry whatever its case; a key without one renders the text alone.
/// </summary>
[HtmlTargetElement("research-term")]
public sealed class ResearchTermTagHelper : TagHelper
{
    private const string KeyPrefix = "Research.Glossary.";

    // The glossary entries of the Research redesign spec (docs/specs/research-redesign.md), keyed case-insensitively.
    private static readonly Dictionary<string, string> Terms = new[]
    {
        "Basket", "Overlap", "Correlation", "Volatility", "Annualized", "Sharpe", "Growth100", "Real", "PublicFloat",
        "Office", "NetAssets", "Fee", "Issuer", "PricedIn"
    }.ToDictionary(term => term, term => KeyPrefix + term, StringComparer.OrdinalIgnoreCase);

    /// <summary>The resource key of every glossary entry, e.g. <c>Research.Glossary.Volatility</c>.</summary>
    internal static IReadOnlyCollection<string> ResourceKeys => Terms.Values;

    private readonly IStringLocalizer _localizer;

    public ResearchTermTagHelper(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>The glossary entry, e.g. <c>volatility</c> for <c>Research.Glossary.Volatility</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        TagHelperContent content = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(content);
        if (!Terms.TryGetValue(Key, out string? resourceKey))
        {
            output.TagName = null;
            return;
        }

        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("class", "research-term");
        output.Attributes.SetAttribute("data-term-text", _localizer[resourceKey].Value);
    }
}
