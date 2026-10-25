using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// PROTOTYPE (#340, throwaway). <c>&lt;js-strings prefix="SavingsGoals.Js." /&gt;</c> hands a page's JS its strings.
/// Two delivery modes, switched for the demo with <c>?js=bundle</c>:
/// <list type="bullet">
/// <item><b>inline</b> (default): <c>&lt;script type="application/json" id="i18n-strings"&gt;</c> holding only the
/// keys under <see cref="Prefix"/>, already resolved through the fallback chain;</item>
/// <item><b>bundle</b>: <c>&lt;meta name="i18n-bundle"&gt;</c> pointing at <c>/i18n/{culture}.{hash}.json</c>, which
/// <c>core/i18n.js</c> fetches (every page's JS keys, cached immutable per hash).</item>
/// </list>
/// </summary>
[HtmlTargetElement("js-strings", TagStructure = TagStructure.WithoutEndTag)]
public sealed class JsStringsTagHelper : TagHelper
{
    private readonly JsonStringLocalizerFactory _factory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JsStringsTagHelper(JsonStringLocalizerFactory factory, IHttpContextAccessor httpContextAccessor)
    {
        _factory = factory;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Key prefix for inline mode, e.g. <c>SavingsGoals.Js.</c>.</summary>
    public string Prefix { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        bool bundle = _httpContextAccessor.HttpContext?.Request.Query["js"].ToString() == "bundle";

        if (bundle)
        {
            Dictionary<string, string> strings = LocalizationPrototypeExtensions.JsBundle(_factory, uiCulture);
            string url = $"/i18n/{uiCulture.Name}.{JsonStringLocalizerFactory.Hash(strings)}.json";
            output.TagName = "meta";
            output.TagMode = TagMode.SelfClosing;
            output.Attributes.SetAttribute("name", "i18n-bundle");
            output.Attributes.SetAttribute("content", url);
            return;
        }

        // Keep Vietnamese readable; the encoder still escapes <, > and & so the JSON cannot close the script element.
        string json = JsonSerializer.Serialize(_factory.Resolve(uiCulture, Prefix),
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });
        output.TagName = "script";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "application/json");
        output.Attributes.SetAttribute("id", "i18n-strings");
        output.Content.SetHtmlContent(json);
    }
}
