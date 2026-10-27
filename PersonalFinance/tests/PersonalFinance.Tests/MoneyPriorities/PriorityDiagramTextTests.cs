using Microsoft.Extensions.Localization;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.MoneyPriorities;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The Mermaid texts Web generates from Priority Tree version 1, in English. Expected classes and <c>linkStyle</c>
/// indices are read off the spec's tree (#315) and its 12 edges in authored order, not derived from the generator.
/// </summary>
public sealed class PriorityDiagramTextTests : IDisposable
{
    private static readonly PriorityTreeDto Tree = MoneyPrioritiesTestServices.TreeV1();
    private static readonly PriorityDiagramPalette Light = PriorityDiagramPalette.Light;
    private static readonly IStringLocalizer L = TestLocalization.Localizer;

    private readonly UiCultureScope _culture = new("en");

    public void Dispose() => _culture.Dispose();

    private static string[] Lines(string text) => text.Split('\n');

    [Fact]
    public void WholeTree_BeforeASubmit_ClassesEveryNodeByItsKind()
    {
        string text = PriorityDiagramText.WholeTree(L, Tree, Light);

        string[] lines = Lines(text);
        Assert.Equal("flowchart TD", lines[0]);
        Assert.Contains("  class S start", lines);
        foreach (string question in new[] { "Q1", "Q2", "Q3", "Q4", "Q5" })
        {
            Assert.Contains($"  class {question} question", lines);
        }

        foreach (string leaf in new[] { "L1", "L2", "L3", "L4", "L5", "L6", "L7" })
        {
            Assert.Contains($"  class {leaf} leaf", lines);
        }

        Assert.DoesNotContain(lines, line => line.Contains("onPath", StringComparison.Ordinal) && line.StartsWith("  class ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("  linkStyle ", StringComparison.Ordinal) && !line.StartsWith("  linkStyle default", StringComparison.Ordinal));
    }

    public static TheoryData<string, string, string> EveryLeafPath => new()
    {
        // Path, then the indices of its edges among the tree's 12 edges in authored order:
        // 0 S-Q1, 1 Q1-L1, 2 Q1-Q2, 3 Q2-L2, 4 Q2-Q3, 5 Q3-L3, 6 Q3-Q4, 7 Q4-L4, 8 Q4-L5, 9 Q4-Q5, 10 Q5-L6, 11 Q5-L7.
        { "L1", "S,Q1,L1", "0,1" },
        { "L2", "S,Q1,Q2,L2", "0,2,3" },
        { "L3", "S,Q1,Q2,Q3,L3", "0,2,4,5" },
        { "L4", "S,Q1,Q2,Q3,Q4,L4", "0,2,4,6,7" },
        { "L5", "S,Q1,Q2,Q3,Q4,L5", "0,2,4,6,8" },
        { "L6", "S,Q1,Q2,Q3,Q4,Q5,L6", "0,2,4,6,9,10" },
        { "L7", "S,Q1,Q2,Q3,Q4,Q5,L7", "0,2,4,6,9,11" }
    };

    private static readonly string[] AllNodeIds =
        ["S", "Q1", "Q2", "Q3", "Q4", "Q5", "L1", "L2", "L3", "L4", "L5", "L6", "L7"];

    [Theory]
    [MemberData(nameof(EveryLeafPath))]
    public void WholeTree_WithAPath_MarksTheLeafChosen_ThePathOnPath_AndTheRestDim(string leaf, string path, string _)
    {
        string[] pathIds = path.Split(',');

        string[] lines = Lines(PriorityDiagramText.WholeTree(L, Tree, pathIds, Light));

        foreach (string id in AllNodeIds)
        {
            string expected = id == leaf ? "chosen" : pathIds.Contains(id) ? "onPath" : "dim";
            Assert.Contains($"  class {id} {expected}", lines);
        }

        Assert.Equal(AllNodeIds.Length, lines.Count(line => line.StartsWith("  class ", StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(EveryLeafPath))]
    public void WholeTree_WithAPath_StylesThePathsEdgesByTheirIndexInTheTree(string _, string path, string edgeIndices)
    {
        string[] lines = Lines(PriorityDiagramText.WholeTree(L, Tree, path.Split(','), Light));

        Assert.Contains("  linkStyle default stroke:#D6DEDB,stroke-width:1px,color:#5B6B78", lines);
        Assert.Contains($"  linkStyle {edgeIndices} stroke:#1D6A50,stroke-width:3px,color:#14283B", lines);
    }

    [Fact]
    public void WholeTree_DrawsEveryEdgeInAuthoredOrder_SoLinkStyleIndicesMatch()
    {
        string[] edgeLines = Lines(PriorityDiagramText.WholeTree(L, Tree, Light))
            .Where(line => line.Contains("-->", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(
            [
                "  S --> Q1",
                "  Q1 -->|\"Yes\"| L1",
                "  Q1 -->|\"No\"| Q2",
                "  Q2 -->|\"No\"| L2",
                "  Q2 -->|\"Yes\"| Q3",
                "  Q3 -->|\"No\"| L3",
                "  Q3 -->|\"Yes\"| Q4",
                "  Q4 -->|\"Under 3 years\"| L4",
                "  Q4 -->|\"3 to 10 years\"| L5",
                "  Q4 -->|\"Over 10 years\"| Q5",
                "  Q5 -->|\"No\"| L6",
                "  Q5 -->|\"Yes\"| L7"
            ],
            edgeLines);
    }

    public static TheoryData<string, string> EveryPathView => new()
    {
        // Path, then the path view's own edge indices: its edges run along the path, so they count up from 0.
        { "S,Q1,L1", "0,1" },
        { "S,Q1,Q2,L2", "0,1,2" },
        { "S,Q1,Q2,Q3,L3", "0,1,2,3" },
        { "S,Q1,Q2,Q3,Q4,L4", "0,1,2,3,4" },
        { "S,Q1,Q2,Q3,Q4,L5", "0,1,2,3,4" },
        { "S,Q1,Q2,Q3,Q4,Q5,L6", "0,1,2,3,4,5" },
        { "S,Q1,Q2,Q3,Q4,Q5,L7", "0,1,2,3,4,5" }
    };

    [Theory]
    [MemberData(nameof(EveryPathView))]
    public void PathView_DrawsExactlyThePathsNodesInOneColumn_StyledByTheViewsOwnEdgeIndices(string path, string ownIndices)
    {
        string[] pathIds = path.Split(',');

        string[] lines = Lines(PriorityDiagramText.PathView(L, Tree, pathIds, Light));

        string[] declaredIds = lines
            .Where(line => line.Contains('"') && !line.Contains("-->", StringComparison.Ordinal)
                && !line.StartsWith("  acc", StringComparison.Ordinal))
            .Select(line => line.TrimStart().Split('(', '[', '{')[0])
            .ToArray();
        Assert.Equal(pathIds, declaredIds);
        Assert.Equal(
            pathIds.Select(id => $"  class {id} " + (id == pathIds[^1] ? "chosen" : "onPath")),
            lines.Where(line => line.StartsWith("  class ", StringComparison.Ordinal)));

        string[] edgeEnds = lines
            .Where(line => line.Contains("-->", StringComparison.Ordinal))
            .Select(line => line.Trim().Split(' ')[0] + ">" + line.Trim().Split(' ')[^1])
            .ToArray();
        Assert.Equal(pathIds.Zip(pathIds.Skip(1), (from, to) => from + ">" + to), edgeEnds);
        Assert.Contains($"  linkStyle {ownIndices} stroke:#1D6A50,stroke-width:3px,color:#14283B", lines);
    }

    [Fact]
    public void PathView_DrawsQuestionsAsRoundedRectangles_AndTheWholeTreeAsDiamonds()
    {
        string[] path = ["S", "Q1", "Q2", "Q3", "L3"];

        string[] pathView = Lines(PriorityDiagramText.PathView(L, Tree, path, Light));
        string[] wholeTree = Lines(PriorityDiagramText.WholeTree(L, Tree, path, Light));

        Assert.Contains("  S([\"Extra money each month\"])", pathView);
        Assert.Contains("  Q1(\"Debt with APR of 7% or more?\")", pathView);
        Assert.Contains("  L3[\"Raise 401(k) contributions to capture the full match\"]", pathView);
        Assert.Contains("  Q1{\"Debt with APR of 7% or more?\"}", wholeTree);
        Assert.Contains("  L3[\"Raise 401(k) contributions to capture the full match\"]", wholeTree);
    }

    [Fact]
    public void EveryText_HasAnAccessibleTitleAndDescription()
    {
        string[] path = ["S", "Q1", "Q2", "L2"];

        string before = PriorityDiagramText.WholeTree(L, Tree, Light);
        string whole = PriorityDiagramText.WholeTree(L, Tree, path, Light);
        string pathView = PriorityDiagramText.PathView(L, Tree, path, Light);

        Assert.Equal(["flowchart TD", "  accTitle: Priority Tree", "  accDescr: The whole tree before any answers"], Lines(before)[..3]);
        Assert.Equal(["flowchart TD", "  accTitle: Priority Tree", "  accDescr: Suggested path ending at: Build the emergency fund to 3 months"], Lines(whole)[..3]);
        Assert.Equal(["flowchart TD", "  accTitle: Priority Tree", "  accDescr: Your path, ending at: Build the emergency fund to 3 months"], Lines(pathView)[..3]);
    }

    [Fact]
    public void DimmedText_UsesTheMutedGreyThatKeepsContrast()
    {
        string text = PriorityDiagramText.WholeTree(L, Tree, ["S", "Q1", "L1"], Light);

        Assert.Contains("  classDef dim fill:#F8FAF9,stroke:#E1E7E4,color:#5B6B78", Lines(text));
        Assert.DoesNotContain("#8796A2", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generators_TakeOnlyTheLocalizerTreeNodeIdsAndAPalette_NeverTheAnswers()
    {
        Type[] webAllowed =
            [typeof(IStringLocalizer), typeof(PriorityTreeDto), typeof(IReadOnlyList<string>), typeof(PriorityDiagramPalette)];
        Type[] apiAllowed = [typeof(PriorityTree), typeof(IReadOnlyList<string>)];

        Type[] webParameters = typeof(PriorityDiagramText)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Append(typeof(MoneyPrioritiesText).GetMethod(nameof(MoneyPrioritiesText.PathSentences))!)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Type[] apiParameters = typeof(PriorityPathSentences)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.NotEmpty(webParameters);
        Assert.All(webParameters, type => Assert.Contains(type, webAllowed));
        Assert.NotEmpty(apiParameters);
        Assert.All(apiParameters, type => Assert.Contains(type, apiAllowed));
    }

    [Theory]
    [InlineData("S,Q1,$500.00")]
    [InlineData("S,Q1,<b>x</b>")]
    [InlineData("S,L5")]
    [InlineData("Q1,L1")]
    [InlineData("S,Q1,Q2")]
    [InlineData("S")]
    [InlineData("")]
    public void APathThatIsNotAWalkOfTheTree_IsRejected(string path)
    {
        string[] pathIds = path.Length == 0 ? [] : path.Split(',');

        Assert.Throws<ArgumentException>(() => PriorityDiagramText.WholeTree(L, Tree, pathIds, Light));
        Assert.Throws<ArgumentException>(() => PriorityDiagramText.PathView(L, Tree, pathIds, Light));
        Assert.Throws<ArgumentException>(() => MoneyPrioritiesText.PathSentences(L, Tree, pathIds));
        Assert.Throws<ArgumentException>(() => PriorityPathSentences.For(PriorityTreeV1.Tree, pathIds));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#FFFFFF;stroke:url(x)")]
    [InlineData("#FFFFFF\n  click S")]
    public void APaletteColourThatIsNotSixDigitHex_IsRejected(string colour)
    {
        PriorityDiagramPalette palette = Light with { Ink = colour };

        Assert.Throws<ArgumentException>(() => PriorityDiagramText.WholeTree(L, Tree, palette));
        Assert.Throws<ArgumentException>(() => PriorityDiagramText.WholeTree(L, Tree, ["S", "Q1", "L1"], palette));
        Assert.Throws<ArgumentException>(() => PriorityDiagramText.PathView(L, Tree, ["S", "Q1", "L1"], palette));
    }

    [Fact]
    public void TheTextsForAnEvaluatedPath_HoldNoneOfTheAnswersValues()
    {
        PriorityAnswers answers = new()
        {
            MonthlyAmount = 98_765.43m,
            DebtApr = 0m,
            EmergencyFundMonths = 11.5m,
            HasFullEmployerMatch = true,
            HorizonYears = 37,
            AgeBand = PersonalFinance.Shared.Enums.RetirementAgeBand.Age60To63,
            AtContributionLimit = false
        };
        PriorityPath path = PriorityPathEvaluator.Evaluate(answers);

        string all = string.Join(
            Environment.NewLine,
            PriorityDiagramText.WholeTree(L, Tree, path.NodeIds, Light),
            PriorityDiagramText.PathView(L, Tree, path.NodeIds, Light),
            string.Join(Environment.NewLine, MoneyPrioritiesText.PathSentences(L, Tree, path.NodeIds)),
            string.Join(Environment.NewLine, PriorityPathSentences.For(PriorityTreeV1.Tree, path.NodeIds)));

        Assert.Equal("L6", path.LeafId);
        foreach (string value in new[] { "98", "765", "11.5", "37", "Age60To63", "60 to 63" })
        {
            Assert.DoesNotContain(value, all, StringComparison.Ordinal);
        }
    }
}
