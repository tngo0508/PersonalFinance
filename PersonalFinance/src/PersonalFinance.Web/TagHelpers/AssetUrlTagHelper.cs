using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PersonalFinance.Web.TagHelpers;

/// <summary>
/// Points <c>script[src]</c> and <c>link[href]</c> references to the app's own JavaScript and CSS (under
/// <c>/js/</c> and <c>/css/</c>) at their minified copies under <c>/dist/</c>, versioned for cache busting.
/// The Release build writes those copies (see the MinifyWebAssets target in the project file).
/// In Development it changes nothing, so pages load the readable originals. <c>/lib/</c>, external URLs
/// and everything else are never touched. Auto-applies through <c>_ViewImports</c>, so views need no edits.
/// </summary>
[HtmlTargetElement("script", Attributes = SrcAttributeName)]
[HtmlTargetElement("link", Attributes = HrefAttributeName)]
public class AssetUrlTagHelper : TagHelper
{
    private const string SrcAttributeName = "src";
    private const string HrefAttributeName = "href";
    private const string DistFolder = "/dist";
    private static readonly string[] AppAssetFolders = ["/js/", "/css/"];

    private readonly IWebHostEnvironment _environment;
    private readonly IFileVersionProvider _fileVersionProvider;

    /// <summary>
    /// Creates the tag helper.
    /// </summary>
    /// <param name="environment">Decides whether to rewrite: never in Development.</param>
    /// <param name="fileVersionProvider">Versions the minified file's URL, as <c>asp-append-version</c> does.</param>
    public AssetUrlTagHelper(IWebHostEnvironment environment, IFileVersionProvider fileVersionProvider)
    {
        _environment = environment;
        _fileVersionProvider = fileVersionProvider;
    }

    /// <summary>
    /// Runs after the built-in Script and Link tag helpers (order -1000), so their resolved,
    /// <c>asp-append-version</c> URL is the one rewritten.
    /// </summary>
    public override int Order => 1000;

    /// <summary>
    /// Gets or sets the view context, used for the request's path base.
    /// </summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (_environment.IsDevelopment())
        {
            return;
        }

        string attributeName = string.Equals(output.TagName, "script", StringComparison.OrdinalIgnoreCase)
            ? SrcAttributeName
            : HrefAttributeName;
        if (!output.Attributes.TryGetAttribute(attributeName, out TagHelperAttribute? attribute))
        {
            return;
        }

        string? url = attribute.Value switch
        {
            string text => text,
            HtmlString html => html.Value,
            _ => null
        };
        string? minifiedUrl = url is null ? null : ToMinifiedUrl(url);
        if (minifiedUrl is null)
        {
            return;
        }

        output.Attributes.SetAttribute(new TagHelperAttribute(attributeName, minifiedUrl, attribute.ValueStyle));
    }

    /// <summary>
    /// Maps an app asset URL (<c>/js/...</c> or <c>/css/...</c> under the path base) to its versioned
    /// <c>/dist/...</c> copy, or returns null for any other URL.
    /// </summary>
    private string? ToMinifiedUrl(string url)
    {
        PathString pathBase = ViewContext.HttpContext.Request.PathBase;
        int queryStart = url.IndexOfAny(['?', '#']);
        string path = queryStart < 0 ? url : url[..queryStart];

        // Only root-relative URLs are the app's own; absolute and protocol-relative URLs point elsewhere
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal)
            || !new PathString(path).StartsWithSegments(pathBase, StringComparison.OrdinalIgnoreCase, out PathString appPath)
            || appPath.Value is not { } appRelativePath
            || !AppAssetFolders.Any(folder => appRelativePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string minifiedPath = pathBase.Add(DistFolder + appRelativePath).Value!;
        return _fileVersionProvider.AddFileVersionToPath(pathBase, minifiedPath);
    }
}
