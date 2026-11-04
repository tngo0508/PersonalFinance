using System.Text.Json;
using System.Text.RegularExpressions;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Watchlist;

/// <summary>
/// The News page's AI summary panel (the inline briefing): the view hands the script the article's URL and an empty
/// panel, the script renders the TL;DR, the key points, why it matters and the coverage of each summary, the
/// stylesheet styles every coverage, and every key the view and the script read exists in en.json.
/// </summary>
public partial class NewsSummaryViewShapeTests
{
    private static readonly string WebProject = RepositoryRoot.SourceProject("PersonalFinance.Web");
    private static readonly string View = File.ReadAllText(Path.Combine(WebProject, "Views", "News", "Index.cshtml"));
    private static readonly string Script = File.ReadAllText(Path.Combine(WebProject, "wwwroot", "js", "news", "news-summary.js"));
    private static readonly string Stylesheet = File.ReadAllText(Path.Combine(WebProject, "wwwroot", "css", "news.css"));

    private static readonly string[] Coverages =
        [NewsSummaryCoverage.FullArticle, NewsSummaryCoverage.FeedExcerpt, NewsSummaryCoverage.HeadlineOnly];

    [GeneratedRegex(@"L(?:\.Named\(|\[)""(?<key>[A-Za-z0-9.]+)""")]
    private static partial Regex LocalizerCall();

    [GeneratedRegex(@"PFI18n\.t\('(?<key>[A-Za-z0-9.]+)'")]
    private static partial Regex ScriptCall();

    private static HashSet<string> EnglishKeys()
    {
        using JsonDocument english = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebProject, "Resources", "i18n", "en.json")));
        return english.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void SummarizeButton_CarriesTheArticleUrl_AndControlsAnEmptyPanelTheScriptFills()
    {
        Assert.Contains("""class="news-action news-summarize-btn" aria-expanded="false" aria-controls="@summaryId" """.TrimEnd(), View, StringComparison.Ordinal);
        Assert.Contains("""data-article-url="@article.Url" data-article-title="@article.Title" """, View, StringComparison.Ordinal);
        Assert.Contains("""<div class="news-summary" id="@summaryId" aria-live="polite" hidden>""", View, StringComparison.Ordinal);
        Assert.Contains("""<div class="news-summary-content"></div>""", View, StringComparison.Ordinal);
        // The footer (coverage, model, link) is the script's, from the response; no fixed disclosure line remains.
        Assert.DoesNotContain("News.Summary.Disclosure", View, StringComparison.Ordinal);
    }

    [Fact]
    public void MetaLine_NamesThePublisherOfAnAggregatorItem_AfterTheSource()
    {
        Assert.Contains("@if (!string.IsNullOrEmpty(article.Publisher))", View, StringComparison.Ordinal);
        Assert.Contains("""L.Named("News.ViaPublisher", ("publisher", article.Publisher))""", View, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_SendsTheArticleUrl_AndRendersEachPartOfTheSummary_WithItsCoverage()
    {
        Assert.Contains("url: articleUrl", Script, StringComparison.Ordinal);
        foreach (string key in new[]
                 {
                     "News.Js.Summary.Tldr", "News.Js.Summary.KeyPoints", "News.Js.Summary.WhyItMatters",
                     "News.Js.Summary.Model", "News.Js.Summary.ReadArticle",
                 })
        {
            Assert.Contains($"PFI18n.t('{key}'", Script, StringComparison.Ordinal);
        }

        Assert.Contains("PFI18n.t(`News.Js.Summary.Coverage.${data.coverage}`)", Script, StringComparison.Ordinal);
        Assert.Contains("PFI18n.t(`News.Js.Summary.CoverageText.${data.coverage}`)", Script, StringComparison.Ordinal);
        Assert.Contains("data.tldr", Script, StringComparison.Ordinal);
        Assert.Contains("data.keyPoints", Script, StringComparison.Ordinal);
        Assert.Contains("data.whyItMatters", Script, StringComparison.Ordinal);
        Assert.Contains("data.coverage", Script, StringComparison.Ordinal);
        // The summary is text from a model: never markup.
        Assert.DoesNotContain("innerHTML", Script, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKeyTheViewAndTheScriptRead_ExistsInEnglish()
    {
        HashSet<string> keys = EnglishKeys();

        List<string> viewKeys = LocalizerCall().Matches(View).Select(m => m.Groups["key"].Value).Distinct().ToList();
        Assert.Contains("News.ViaPublisher", viewKeys);
        Assert.DoesNotContain(viewKeys, key => !keys.Contains(key));

        List<string> scriptKeys = ScriptCall().Matches(Script).Select(m => m.Groups["key"].Value).Distinct().ToList();
        Assert.Contains("News.Js.Summary.Tldr", scriptKeys);
        Assert.DoesNotContain(scriptKeys, key => !keys.Contains(key));

        foreach (string coverage in Coverages)
        {
            Assert.Contains($"News.Js.Summary.Coverage.{coverage}", keys);
            Assert.Contains($"News.Js.Summary.CoverageText.{coverage}", keys);
        }
    }

    [Fact]
    public void Stylesheet_StylesTheCoverageChipForEachCoverage_AndTheLoadingSkeleton()
    {
        foreach (string coverage in Coverages)
        {
            Assert.Contains($""".news-summary-coverage[data-coverage="{coverage}"]""", Stylesheet, StringComparison.Ordinal);
        }

        Assert.Contains(".news-summary-skeleton", Stylesheet, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", Stylesheet, StringComparison.Ordinal);
    }
}
