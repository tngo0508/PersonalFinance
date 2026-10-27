using System.Reflection;
using Microsoft.Extensions.Localization;
using PersonalFinance.ApiService.Features.MoneyPriorities;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.Models.MoneyPriorities;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The Priority Tree's wording in the request's Language (#382): every node and answer of every authored tree version
/// has an English and a Vietnamese entry, so a new version cannot ship untranslated, and under <c>vi</c> the "Your
/// path" sentences and both diagrams hold the Vietnamese wording, not the ApiService's English.
/// </summary>
public class PriorityTreeTextLocalizationTests
{
    private static readonly string[] FrameKeys =
    [
        "MoneyPriorities.Path.Step",
        "MoneyPriorities.Path.Suggested",
        "MoneyPriorities.Diagram.Title",
        "MoneyPriorities.Diagram.WholeTree",
        "MoneyPriorities.Diagram.WholeTreePath",
        "MoneyPriorities.Diagram.PathView"
    ];

    private static readonly string[] EveryLeafPath =
    [
        "S,Q1,L1", "S,Q1,Q2,L2", "S,Q1,Q2,Q3,L3", "S,Q1,Q2,Q3,Q4,L4", "S,Q1,Q2,Q3,Q4,L5", "S,Q1,Q2,Q3,Q4,Q5,L6",
        "S,Q1,Q2,Q3,Q4,Q5,L7"
    ];

    /// <summary>The short English answers, as the diagrams quote them: too short to search for unquoted.</summary>
    private static readonly string[] QuotedEnglishAnswers = ["\"Yes\"", "\"No\""];

    /// <summary>Every authored tree: each <c>PriorityTreeV&lt;n&gt;</c> class's <c>Tree</c>, found by reflection.</summary>
    public static TheoryData<int> AuthoredTreeVersions => new(AuthoredTrees().Select(tree => tree.Version));

    [Fact]
    public void AuthoredTrees_AreFound()
    {
        Assert.Contains(PriorityTreeV1.Version, AuthoredTrees().Select(tree => tree.Version));
    }

    [Theory]
    [MemberData(nameof(AuthoredTreeVersions))]
    public void EveryNodeAndAnswer_HasAnEnglishEntryMatchingTheTree_AndAVietnameseEntry(int version)
    {
        PriorityTreeDto tree = PriorityWalkMapping.ToDto(AuthoredTrees().Single(t => t.Version == version));
        List<(string Key, string English)> entries = tree.Nodes
            .Select(node => (MoneyPrioritiesText.NodeKey(node), node.Label))
            .Concat(tree.Edges.Where(edge => edge.Label is not null)
                .Select(edge => (MoneyPrioritiesText.EdgeKey(edge), edge.Label!)))
            .ToList();

        List<string> problems = [];
        foreach ((string key, string english) in entries)
        {
            if (!TestLocalization.Catalog.TryGet("en", key, out string en))
            {
                problems.Add($"en.json has no {key}.");
            }
            else if (en != english)
            {
                problems.Add($"en.json {key} is \"{en}\", but tree version {version} says \"{english}\".");
            }

            if (!TestLocalization.Catalog.TryGet("vi", key, out string _))
            {
                problems.Add($"vi.json has no {key}.");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void ThePathAndDiagramFrames_HaveAVietnameseEntry()
    {
        Assert.All(FrameKeys, key => TestLocalization.Vietnamese(key));
    }

    [Fact]
    public void InVietnamese_ThePathSentences_UseTheVietnameseQuestionsAnswersAndLeaf()
    {
        using var culture = new UiCultureScope("vi");
        PriorityTreeDto tree = MoneyPrioritiesTestServices.TreeV1();

        List<string> sentences = MoneyPrioritiesText.PathSentences(TestLocalization.Localizer, tree, ["S", "Q1", "Q2", "L2"]);

        Assert.Equal(
            [
                Step(TestLocalization.Vietnamese("MoneyPriorities.Node.Q1"), TestLocalization.Vietnamese("MoneyPriorities.Edge.Q1.Q2")),
                Step(TestLocalization.Vietnamese("MoneyPriorities.Node.Q2"), TestLocalization.Vietnamese("MoneyPriorities.Edge.Q2.L2")),
                TestLocalization.Vietnamese("MoneyPriorities.Path.Suggested")
                    .Replace("{leaf}", TestLocalization.Vietnamese("MoneyPriorities.Leaf.L2"), StringComparison.Ordinal)
            ],
            sentences);
    }

    [Fact]
    public void InVietnamese_EveryResult_HoldsNoEnglishTreeWording()
    {
        using var culture = new UiCultureScope("vi");
        PriorityTreeDto tree = MoneyPrioritiesTestServices.TreeV1();
        string[] english = tree.Nodes.Select(node => node.Label)
            .Concat(tree.Edges.Select(edge => edge.Label).OfType<string>())
            .Where(label => label.Length > 3)
            .Append("Suggested priority")
            .Append("Priority Tree")
            .ToArray();

        foreach (string path in EveryLeafPath)
        {
            string[] ids = path.Split(',');
            string all = string.Join('\n',
                PriorityDiagramText.WholeTree(TestLocalization.Localizer, tree, PriorityDiagramPalette.Light),
                PriorityDiagramText.WholeTree(TestLocalization.Localizer, tree, ids, PriorityDiagramPalette.Light),
                PriorityDiagramText.PathView(TestLocalization.Localizer, tree, ids, PriorityDiagramPalette.Light),
                string.Join('\n', MoneyPrioritiesText.PathSentences(TestLocalization.Localizer, tree, ids)));

            Assert.All(english, text => Assert.DoesNotContain(text, all, StringComparison.Ordinal));
            Assert.All(QuotedEnglishAnswers, text => Assert.DoesNotContain(text, all, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void InVietnamese_TheDiagramLabelsEveryNodeAndAnswer_InVietnamese()
    {
        using var culture = new UiCultureScope("vi");
        PriorityTreeDto tree = MoneyPrioritiesTestServices.TreeV1();

        string[] lines = PriorityDiagramText.WholeTree(TestLocalization.Localizer, tree, PriorityDiagramPalette.Light).Split('\n');

        Assert.Equal("  accTitle: " + TestLocalization.Vietnamese("MoneyPriorities.Diagram.Title"), lines[1]);
        Assert.Contains($"  Q1{{\"{Mermaid(TestLocalization.Vietnamese("MoneyPriorities.Node.Q1"))}\"}}", lines);
        Assert.Contains($"  L6[\"{Mermaid(TestLocalization.Vietnamese("MoneyPriorities.Leaf.L6"))}\"]", lines);
        Assert.Contains($"  Q4 -->|\"{Mermaid(TestLocalization.Vietnamese("MoneyPriorities.Edge.Q4.L4"))}\"| L4", lines);
    }

    [Fact]
    public void ANodeThePageHasNoKeyFor_KeepsTheApiServiceWording()
    {
        IStringLocalizer localizer = LocalizationTestCatalog.Localizer(
            """{ "MoneyPriorities.Path.Step": "{question} {answer}.", "MoneyPriorities.Path.Suggested": "Suggested priority: {leaf}." }""",
            """{ "MoneyPriorities.Path.Step": "{question} {answer}.", "MoneyPriorities.Path.Suggested": "Ưu tiên gợi ý: {leaf}." }""");
        using var culture = new UiCultureScope("vi");

        List<string> sentences = MoneyPrioritiesText.PathSentences(localizer, LaterTree(), ["S", "Q9", "L9"]);

        Assert.Equal(["A question from a later tree? Maybe.", "Ưu tiên gợi ý: A leaf from a later tree."], sentences);
    }

    [Fact]
    public void ATranslationWithAQuoteOrALineBreak_StaysInsideItsLabel()
    {
        IStringLocalizer localizer = LocalizationTestCatalog.Localizer(
            """{ "MoneyPriorities.Node.Q9": "x" }""",
            """{ "MoneyPriorities.Node.Q9": "Say \"hi\"\n  click S", "MoneyPriorities.Diagram.Title": "Cây\nclick S" }""");
        using var culture = new UiCultureScope("vi");

        string[] lines = PriorityDiagramText.WholeTree(localizer, LaterTree(), PriorityDiagramPalette.Light).Split('\n');

        Assert.Contains("  Q9{\"Say #quot;hi#quot; click S\"}", lines);
        Assert.Equal("  accTitle: Cây click S", lines[1]);
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith("click", StringComparison.Ordinal));
    }

    private static string Step(string question, string answer) =>
        TestLocalization.Vietnamese("MoneyPriorities.Path.Step")
            .Replace("{question}", question, StringComparison.Ordinal)
            .Replace("{answer}", answer, StringComparison.Ordinal);

    private static string Mermaid(string label) => label.Replace("\"", "#quot;", StringComparison.Ordinal);

    private static PriorityTreeDto LaterTree() => new()
    {
        Version = 99,
        Nodes =
        [
            new() { Id = "S", Kind = PriorityNodeKind.Start, Label = "Extra money each month" },
            new() { Id = "Q9", Kind = PriorityNodeKind.Question, Label = "A question from a later tree?" },
            new() { Id = "L9", Kind = PriorityNodeKind.Leaf, Label = "A leaf from a later tree" }
        ],
        Edges =
        [
            new() { From = "S", To = "Q9" },
            new() { From = "Q9", To = "L9", Label = "Maybe" }
        ]
    };

    private static IEnumerable<PriorityTree> AuthoredTrees() =>
        typeof(PriorityTree).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(PriorityTree).Namespace && type.Name.StartsWith("PriorityTreeV", StringComparison.Ordinal))
            .Select(type => type.GetProperty("Tree", BindingFlags.Public | BindingFlags.Static)?.GetValue(null))
            .OfType<PriorityTree>();
}
