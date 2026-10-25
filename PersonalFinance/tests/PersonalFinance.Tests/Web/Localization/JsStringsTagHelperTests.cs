using System.Text.Json;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.TagHelpers;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>The <c>&lt;js-strings&gt;</c> JSON island: the page's keys only, resolved, safe inside a script element.</summary>
public class JsStringsTagHelperTests
{
    private const string En = """
        {
          "Goals.Js.Confirm": "Delete \"{name}\"?",
          "Goals.Js.OnlyEnglish": "English only",
          "Goals.Js.Markup": "</script><b>",
          "Goals.Title": "Goals",
          "Budget.Js.Save": "Save"
        }
        """;

    private const string Vi = """{ "Goals.Js.Confirm": "Xóa \"{name}\"?", "Goals.Title": "Mục tiêu" }""";

    [Fact]
    public void RendersOnlyKeysUnderThePrefix_ResolvedThroughTheChain()
    {
        using var culture = new UiCultureScope("vi");

        TagHelperOutput output = Render("Goals.Js.");

        Dictionary<string, string> strings = Parse(output);
        Assert.Equal(["Goals.Js.Confirm", "Goals.Js.Markup", "Goals.Js.OnlyEnglish"], strings.Keys.Order());
        Assert.Equal("Xóa \"{name}\"?", strings["Goals.Js.Confirm"]);
        Assert.Equal("English only", strings["Goals.Js.OnlyEnglish"]);
    }

    [Fact]
    public void RendersAJsonScriptIsland_ReadableVietnamese_AndNoClosingTag()
    {
        using var culture = new UiCultureScope("vi");

        TagHelperOutput output = Render("Goals.Js.");

        Assert.Equal("script", output.TagName);
        Assert.Equal("application/json", output.Attributes["type"].Value);
        Assert.Equal(JsStringsTagHelper.ElementId, output.Attributes["id"].Value);
        string json = output.Content.GetContent();
        Assert.Contains("Xóa", json);
        Assert.DoesNotContain("</script>", json);
        Assert.DoesNotContain("<b>", json);
    }

    [Fact]
    public void SeveralPrefixes_AreCommaSeparated()
    {
        using var culture = new UiCultureScope("en");

        Dictionary<string, string> strings = Parse(Render("Goals.Js., Budget.Js."));

        Assert.Contains("Budget.Js.Save", strings.Keys);
        Assert.Contains("Goals.Js.Confirm", strings.Keys);
        Assert.DoesNotContain("Goals.Title", strings.Keys);
    }

    [Fact]
    public void NoPrefix_RendersAnEmptyIsland()
    {
        using var culture = new UiCultureScope("en");

        Assert.Empty(Parse(Render(string.Empty)));
    }

    private static TagHelperOutput Render(string prefix)
    {
        var helper = new JsStringsTagHelper(LocalizationTestCatalog.Localizer(En, Vi)) { Prefix = prefix };
        var context = new TagHelperContext([], new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput("js-strings", [],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(context, output);
        return output;
    }

    private static Dictionary<string, string> Parse(TagHelperOutput output) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(output.Content.GetContent())!;
}
