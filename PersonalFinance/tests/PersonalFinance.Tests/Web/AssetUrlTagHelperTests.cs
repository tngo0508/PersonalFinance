using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using PersonalFinance.Web.TagHelpers;
using Xunit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// Outside Development, pages reference the minified copies under /dist with a version for cache busting.
/// In Development they keep the readable originals. Third-party and external URLs are never rewritten.
/// </summary>
public class AssetUrlTagHelperTests
{
    private const string DistVersion = "dist-file-hash";

    [Theory]
    [InlineData("script", "src", "/js/core/utils.js?v=source-hash")]
    [InlineData("link", "href", "/css/site.css?v=source-hash")]
    public void Development_LeavesAppAssetUrlsUntouched(string tagName, string attributeName, string url)
    {
        string rendered = Render("Development", tagName, attributeName, url);

        Assert.Equal(url, rendered);
    }

    [Fact]
    public void Production_PointsScriptAtMinifiedCopyWithItsOwnVersion()
    {
        string rendered = Render("Production", "script", "src", "/js/core/utils.js?v=source-hash");

        Assert.Equal("/dist/js/core/utils.js?v=" + DistVersion, rendered);
    }

    [Fact]
    public void Production_PointsStylesheetAtMinifiedCopyWithItsOwnVersion()
    {
        string rendered = Render("Production", "link", "href", "/css/site.css?v=source-hash");

        Assert.Equal("/dist/css/site.css?v=" + DistVersion, rendered);
    }

    [Fact]
    public void Production_VersionsAppAssetsAuthoredWithoutAVersion()
    {
        string rendered = Render("Production", "script", "src", "/js/landing/skyline.js");

        Assert.Equal("/dist/js/landing/skyline.js?v=" + DistVersion, rendered);
    }

    [Fact]
    public void Production_KeepsTheAppPathBase()
    {
        string rendered = Render("Production", "script", "src", "/app/js/core/utils.js?v=source-hash", pathBase: "/app");

        Assert.Equal("/app/dist/js/core/utils.js?v=" + DistVersion, rendered);
    }

    [Theory]
    [InlineData("script", "src", "/lib/jquery/dist/jquery.min.js?v=lib-hash")]
    [InlineData("link", "href", "/lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("script", "src", "https://cdn.example.com/js/core/utils.js")]
    [InlineData("script", "src", "//cdn.example.com/js/core/utils.js")]
    [InlineData("link", "href", "https://fonts.googleapis.com/css/family.css")]
    [InlineData("link", "href", "/favicon.ico")]
    public void Production_NeverRewritesThirdPartyOrExternalUrls(string tagName, string attributeName, string url)
    {
        string rendered = Render("Production", tagName, attributeName, url);

        Assert.Equal(url, rendered);
    }

    private static string Render(string environmentName, string tagName, string attributeName, string url, string pathBase = "")
    {
        var tagHelper = new AssetUrlTagHelper(new FakeWebHostEnvironment(environmentName), new FakeFileVersionProvider())
        {
            ViewContext = CreateViewContext(pathBase)
        };
        var attributes = new TagHelperAttributeList { { attributeName, url } };
        var context = new TagHelperContext(tagName, attributes, new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(tagName, new TagHelperAttributeList(attributes),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        tagHelper.Process(context, output);

        return output.Attributes[attributeName].Value.ToString()!;
    }

    private static ViewContext CreateViewContext(string pathBase)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = pathBase;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        return new ViewContext { HttpContext = httpContext, RouteData = actionContext.RouteData, ViewData = viewData };
    }

    /// <summary>Stands in for the file-version provider: versions a path the way it would, with a known hash.</summary>
    private sealed class FakeFileVersionProvider : IFileVersionProvider
    {
        public string AddFileVersionToPath(PathString requestPathBase, string path) => path + "?v=" + DistVersion;
    }

    private sealed class FakeWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "PersonalFinance.Web";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
