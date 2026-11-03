using System.Text.Json;
using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// The Financial Universe view's shape for the graph (spec #617 slice 2, ADR 0020): the pinned scripts load from the
/// view's Scripts section, the canvas is labelled for assistive technology with the list as its equivalent, the toggle
/// is a pair of pressed buttons, the panels hide with the attribute the stylesheet guards, and every key the view reads
/// exists in en.json.
/// </summary>
public partial class FinancialUniverseViewShapeTests
{
    private static readonly string WebProject = RepositoryRoot.SourceProject("PersonalFinance.Web");
    private static readonly string View = File.ReadAllText(Path.Combine(WebProject, "Views", "FinancialUniverse", "Index.cshtml"));

    [GeneratedRegex(@"L(?:\.Named\(|\[)""(?<key>[A-Za-z0-9.]+)""")]
    private static partial Regex LocalizerCall();

    [Fact]
    public void ScriptsSection_LoadsThePinnedLibraries_ThenTheRenderer()
    {
        int scripts = View.IndexOf("@section Scripts", StringComparison.Ordinal);
        Assert.True(scripts >= 0, "the view has no Scripts section");
        string section = View[scripts..];

        int cytoscape = section.IndexOf("~/lib/cytoscape/dist/cytoscape.min.js", StringComparison.Ordinal);
        int dagre = section.IndexOf("~/lib/cytoscape-dagre/dist/cytoscape-dagre.min.js", StringComparison.Ordinal);
        int renderer = section.IndexOf("~/js/financial-universe/graph.js", StringComparison.Ordinal);
        Assert.True(cytoscape >= 0 && dagre > cytoscape && renderer > dagre,
            "Cytoscape, then its dagre extension, then graph.js must load from the Scripts section in that order");
        Assert.Contains("~/css/financial-universe.css", View, StringComparison.Ordinal);
    }

    [Fact]
    public void Canvas_IsAnImage_WithALabelNamingTheMonth()
    {
        Assert.Contains("id=\"fuGraph\" role=\"img\"", View, StringComparison.Ordinal);
        Assert.Contains("""aria-label="@L.Named("FinancialUniverse.Graph.Label", ("month", Model.MonthLabel))" """, View, StringComparison.Ordinal);
    }

    [Fact]
    public void Toggle_IsAPairOfPressedButtons_AndTheListShowsWithoutScript()
    {
        Assert.Contains("data-fu-view=\"graph\" aria-pressed=\"false\"", View, StringComparison.Ordinal);
        Assert.Contains("data-fu-view=\"list\" aria-pressed=\"true\"", View, StringComparison.Ordinal);
        Assert.Contains("id=\"fuGraphPanel\"", View, StringComparison.Ordinal);
        Assert.Contains("data-test-id=\"fu-graph-panel\" hidden", View, StringComparison.Ordinal);
        Assert.Contains("id=\"fuListPanel\"", View, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"fuListPanel\" data-test-id=\"fu-list-panel\" hidden", View, StringComparison.Ordinal);

        string stylesheet = File.ReadAllText(Path.Combine(WebProject, "wwwroot", "css", "financial-universe.css"));
        Assert.Contains(".fu-panel[hidden]", stylesheet, StringComparison.Ordinal);
        Assert.Contains(".fu-toolbar [hidden]", stylesheet, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKeyTheViewReads_ExistsInEnglish()
    {
        using JsonDocument english = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebProject, "Resources", "i18n", "en.json")));
        HashSet<string> keys = english.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

        List<string> read = LocalizerCall().Matches(View).Select(match => match.Groups["key"].Value).Distinct().ToList();
        Assert.Contains("FinancialUniverse.Graph.Legend", read);
        Assert.Contains("FinancialUniverse.View.List", read);
        Assert.DoesNotContain(read, key => !keys.Contains(key));
    }
}
