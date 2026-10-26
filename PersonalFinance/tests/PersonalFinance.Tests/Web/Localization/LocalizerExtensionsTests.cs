using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>Named placeholders, markup placeholders and plurals (spec §2.1).</summary>
public class LocalizerExtensionsTests
{
    private const string En = """
        {
          "Goal.Progress": "{current} of {target}",
          "Goal.Count.one": "{count} goal",
          "Goal.Count.other": "{count} goals",
          "Goal.Named": "{name} saved {amount}"
        }
        """;

    // The Vietnamese puts the placeholders in the other order, which named placeholders must allow.
    private const string Vi = """
        {
          "Goal.Progress": "Mục tiêu {target}, đã có {current}",
          "Goal.Count.other": "{count} mục tiêu",
          "Goal.Named": "{amount} đã được {name} tiết kiệm"
        }
        """;

    [Fact]
    public void Named_MatchesPlaceholdersByName_InTheTranslationsOrder()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        using var culture = new UiCultureScope("vi");

        Assert.Equal("$5 đã được An tiết kiệm", localizer.Named("Goal.Named", ("name", "An"), ("amount", "$5")));
    }

    [Fact]
    public void Named_LeavesAnUnknownPlaceholderAsWritten()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        using var culture = new UiCultureScope("en");

        Assert.Equal("An saved {amount}", localizer.Named("Goal.Named", ("name", "An")));
    }

    [Fact]
    public void NamedHtml_InsertsMarkupWhereTheTranslationPutsIt_AndEncodesText()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        using var culture = new UiCultureScope("vi");

        string html = Render(localizer.NamedHtml("Goal.Progress", "sep",
            ("current", new HtmlString("<b>1</b>")), ("target", new HtmlString("<b>2</b>"))));

        Assert.Equal("<span class=\"sep\">Mục tiêu</span><b>2</b><span class=\"sep\">, đã có</span><b>1</b>", html);
    }

    [Fact]
    public void NamedHtml_EncodesLiteralTextFromTheTranslation()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer("""{ "Goal.Evil": "<script>{x}" }""", "{}");
        using var culture = new UiCultureScope("en");

        string html = Render(localizer.NamedHtml("Goal.Evil", null, ("x", new HtmlString("<i>ok</i>"))));

        Assert.Equal("&lt;script&gt;<i>ok</i>", html);
    }

    [Theory]
    [InlineData("en", 1, "1 goal")]
    [InlineData("en", 0, "0 goals")]
    [InlineData("en", 1234, "1,234 goals")]
    [InlineData("vi", 1, "1 mục tiêu")]
    [InlineData("vi", 3, "3 mục tiêu")]
    public void Plural_PicksTheCategoryOfTheLanguage(string uiCulture, int count, string expected)
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        // The count is formatted in the language's formatting culture, so set both, as a request does.
        using var language = LanguageScope.Use(AppLanguages.Find(uiCulture)!);

        Assert.Equal(expected, localizer.Plural("Goal.Count", count));
    }

    [Fact]
    public void Plural_MissingCategory_FallsBackToOther()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer("""{ "Goal.Count.other": "{count} goals" }""", "{}");
        using var culture = new UiCultureScope("en");

        Assert.Equal("1 goals", localizer.Plural("Goal.Count", 1));
    }

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));
        return writer.ToString();
    }
}
