using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.TagHelpers;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// <c>&lt;research-term key="..."&gt;</c>: a glossary term becomes a button carrying its explanation in the request's
/// Language, which <c>research-glossary.js</c> shows on click; a key without a glossary entry leaves the text as it is.
/// </summary>
public class ResearchTermTagHelperTests
{
    private const string En = """
        {
          "Research.Glossary.Volatility": "Volatility: how much monthly returns swung, shown as a yearly figure.",
          "Research.Glossary.NetAssets": "Net assets: the fund's total size & \"bubble\" area."
        }
        """;

    private const string Vi = """{ "Research.Glossary.Volatility": "Độ biến động: mức dao động của lợi nhuận." }""";

    [Fact]
    public async Task KnownKey_RendersAButtonWithTheLocalizedExplanation_AndKeepsTheText()
    {
        using var culture = new UiCultureScope("vi");

        string html = await RenderAsync("volatility", "Volatility");

        Assert.Equal(
            """<button type="button" class="research-term" data-term-text="Độ biến động: mức dao động của lợi nhuận.">Volatility</button>""",
            html);
    }

    [Fact]
    public async Task KnownKey_MatchesTheEntryWhateverItsCase_AndEncodesTheExplanation()
    {
        using var culture = new UiCultureScope("en");

        string html = await RenderAsync("netAssets", "net assets");

        Assert.Equal(
            """<button type="button" class="research-term" data-term-text="Net assets: the fund&#x27;s total size &amp; &quot;bubble&quot; area.">net assets</button>""",
            html);
    }

    [Theory]
    [InlineData("leverage")]
    [InlineData("")]
    [InlineData("Glossary.Volatility")]
    public async Task UnknownKey_RendersTheContentOnly(string key)
    {
        using var culture = new UiCultureScope("en");

        string html = await RenderAsync(key, "Leverage");

        Assert.Equal("Leverage", html);
    }

    private static async Task<string> RenderAsync(string key, string text)
    {
        var helper = new ResearchTermTagHelper(LocalizationTestCatalog.Localizer(En, Vi)) { Key = key };
        var context = new TagHelperContext([], new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput("research-term", [], (_, _) =>
        {
            TagHelperContent content = new DefaultTagHelperContent();
            content.SetContent(text);
            return Task.FromResult(content);
        });
        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        // The app encodes with every Unicode range allowed (LocalizationServiceExtensions), so Vietnamese stays readable.
        output.WriteTo(writer, HtmlEncoder.Create(UnicodeRanges.All));
        return writer.ToString();
    }
}
