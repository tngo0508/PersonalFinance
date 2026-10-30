using System.Text.Json;
using System.Text.Json.Nodes;
using PersonalFinance.Tests.Learning.ContentChecks;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// The Atelier content checks (spec #513, ADR 0019) over the real content folder, which must always pass, and over the
/// fixture folder: as written it passes, and each check rejects a copy broken in one place, naming the file and field.
/// </summary>
public class LearningContentChecksTests
{
    private static readonly ContentRules Rules = ContentRules.Read(LearningContentFolders.Real);

    private const string LongSentence =
        "This sentence keeps on going with one more word and then another word and another one until it is far longer than the limit of the rule allows.";

    private static readonly string LongNote = string.Join(" ", Enumerable.Repeat("Banks must show this rate to borrowers.", 10));

    private static readonly Dictionary<string, Mutation> Mutations = new()
    {
        // Term Entries: fields and lengths
        ["missing term"] = new("terms/apr.json", "term", e => e.Remove("term")),
        ["missing Vietnamese name"] = new("terms/apr.json", "vietnameseName", e => e.Remove("vietnameseName")),
        ["missing last reviewed"] = new("terms/apr.json", "lastReviewed", e => e.Remove("lastReviewed")),
        ["unknown topic"] = new("terms/apr.json", "topic", e => e["topic"] = "crypto"),
        ["one-liner over 15 words"] = new("terms/apr.json", "oneLine",
            e => e["oneLine"] = "The yearly cost of borrowing money, shown as one percent that adds up the interest and fees."),
        ["entry under 100 words"] = new("terms/credit-score.json", "(entry)", e =>
        {
            e["explanation"] = new JsonArray("A credit score is a number.");
            e.Remove("inTheUs");
        }),
        ["entry over 250 words"] = new("terms/apr.json", "(entry)", e =>
            e["explanation"] = new JsonArray(Enumerable.Repeat(
                "APR shows the cost of a loan in one year. It includes the interest and some fees.", 12)
                .Select(p => (JsonNode?)p).ToArray())),
        ["sentence of 25 words"] = new("terms/apr.json", "whyItMatters", e => e["whyItMatters"] = LongSentence),
        ["country note over 60 words"] = new("terms/apr.json", "inVietnam.text", e => e["inVietnam"]!["text"] = LongNote),
        ["country note without its year"] = new("terms/apr.json", "inTheUs.rulesAsOf",
            e => e["inTheUs"]!.AsObject().Remove("rulesAsOf")),

        // Term Entries: sources
        ["no sources"] = new("terms/apr.json", "sources", e => e["sources"] = new JsonArray()),
        ["citation not allowlisted"] = new("terms/apr.json", "sources[0].url",
            e => e["sources"]![0]!["url"] = "https://example.com/apr"),
        ["citation of a link-only source"] = new("terms/apr.json", "sources[0].url",
            e => e["sources"]![0]!["url"] = "https://www.finra.org/investors/insights/apr"),
        ["IRS citation without its tax year"] = new("terms/apr.json", "sources[0].taxYear",
            e => e["sources"]![0]!["url"] = "https://www.irs.gov/taxtopics/tc505"),
        ["citation without its access date"] = new("terms/apr.json", "sources[0].accessed",
            e => e["sources"]![0]!.AsObject().Remove("accessed")),
        ["country note citing only the other country"] = new("terms/apr.json", "inVietnam.sources",
            e => e["inVietnam"]!["sources"]![0]!["url"] = "https://www.consumerfinance.gov/ask-cfpb/apr/"),
        ["further reading not allowlisted"] = new("terms/credit-score.json", "furtherReading[0].url",
            e => e["furtherReading"]![0]!["url"] = "https://example.com/credit"),
        ["video not allowlisted"] = new("terms/compound-interest.json", "video.url",
            e => e["video"]!["url"] = "https://www.youtube.com/watch?v=fixture0001"),

        // Term Entries: slugs, references and names
        ["slug unlike the file name"] = new("terms/apr.json", "slug", e => e["slug"] = "annual-percentage-rate"),
        ["slug not in kebab case"] = new("terms/Credit_Score.json", "slug", e => e["slug"] = "Credit_Score"),
        ["translation without an English file"] = new("terms/mortgage.vi.json", "slug", e => e["slug"] = "mortgage"),
        ["related term that does not exist"] = new("terms/apr.json", "relatedTerms[0]",
            e => e["relatedTerms"]![0] = "mortgage"),
        ["related to itself"] = new("terms/apr.json", "relatedTerms[0]", e => e["relatedTerms"]![0] = "apr"),
        ["no related terms"] = new("terms/apr.json", "relatedTerms", e => e["relatedTerms"] = new JsonArray()),
        ["six related terms"] = new("terms/apr.json", "relatedTerms", e => e["relatedTerms"] =
            new JsonArray("credit-score", "compound-interest", "emergency-fund", "a", "b", "c")),
        ["alias of another entry"] = new("terms/credit-score.json", "alsoKnownAs[0]",
            e => e["alsoKnownAs"] = new JsonArray("apr")),
        ["Term of another entry"] = new("terms/emergency-fund.json", "term", e => e["term"] = "credit score"),

        // Plain English
        ["advice phrasing"] = new("terms/apr.json", "whyItMatters",
            e => e["whyItMatters"] = "You should pick the loan with the lower APR."),
        ["listed idiom"] = new("terms/emergency-fund.json", "workedExample",
            e => e["workedExample"] = "Save $250 a month as a nest egg for later."),
        ["advice phrasing in a Check"] = new("lessons/borrow-wisely.json", "check[0].explanation",
            e => e["check"]![0]!["explanation"] = "Myth. We recommend a low APR."),

        // Lessons
        ["Lesson with one Term Entry"] = new("lessons/build-a-cushion.json", "terms",
            e => e["terms"] = new JsonArray("emergency-fund")),
        ["Lesson with four Term Entries"] = new("lessons/build-a-cushion.json", "terms",
            e => e["terms"] = new JsonArray("emergency-fund", "compound-interest", "apr", "credit-score")),
        ["Lesson term that does not exist"] = new("lessons/build-a-cushion.json", "terms[1]",
            e => e["terms"]![1] = "mortgage"),
        ["Term Entry in two Lessons"] = new("lessons/borrow-wisely.json", "terms[1]",
            e => e["terms"]![1] = "emergency-fund"),
        ["gap in the days"] = new("lessons/borrow-wisely.json", "day", e => e["day"] = 3),
        ["repeated day"] = new("lessons/borrow-wisely.json", "day", e => e["day"] = 1),
        ["intro of one sentence"] = new("lessons/build-a-cushion.json", "intro", e => e["intro"] = "Life brings surprises."),
        ["Check with one question"] = new("lessons/build-a-cushion.json", "check",
            e => e["check"]!.AsArray().RemoveAt(1)),
        ["unknown question format"] = new("lessons/build-a-cushion.json", "check[0].format",
            e => e["check"]![0]!["format"] = "true-or-false"),
        ["Myth or fact with three options"] = new("lessons/build-a-cushion.json", "check[0].options",
            e => e["check"]![0]!["options"] = new JsonArray("Myth", "Fact", "Both")),
        ["What happens next with two options"] = new("lessons/build-a-cushion.json", "check[1].options",
            e => e["check"]![1]!["options"] = new JsonArray("More", "Less")),
        ["option over 8 words"] = new("lessons/build-a-cushion.json", "check[1].options[0]",
            e => e["check"]![1]!["options"]![0] = "She has exactly the same one thousand dollars as before"),
        ["correct option out of range"] = new("lessons/build-a-cushion.json", "check[0].correctOption",
            e => e["check"]![0]!["correctOption"] = 2),
        ["prompt of three sentences"] = new("lessons/build-a-cushion.json", "check[0].prompt",
            e => e["check"]![0]!["prompt"] = "Lan has a fund. She uses it. Is it for holidays?"),
        ["explanation of three sentences"] = new("lessons/build-a-cushion.json", "check[0].explanation",
            e => e["check"]![0]!["explanation"] = "Myth. It is for surprises. It is not for holidays."),
        ["Try it outside the app"] = new("lessons/build-a-cushion.json", "tryIt.url",
            e => e["tryIt"]!["url"] = "https://example.com/savings"),
        ["missing keepsake name"] = new("lessons/build-a-cushion.json", "keepsake.name",
            e => e["keepsake"]!.AsObject().Remove("name")),
    };

    public static TheoryData<string> MutationNames => new(Mutations.Keys);

    // ATELIER_CONTENT_DIR points this at a dry-run copy instead (the /atelier-draft skill); the allowlist and
    // plain-English lists still come from the real folder.
    [Fact]
    public void TheRealContentFolder_PassesEveryCheck()
    {
        AssertNoProblems(LearningContentChecker.Check(LearningContentFolders.UnderCheck, Rules));
    }

    [Fact]
    public void TheAtelierDraftSample_PassesEveryCheck_BesideTheFixture()
    {
        using LearningContentFolders.TempFolder folder = LearningContentFolders.CopyOfFixture();
        File.Copy(Path.Combine(LearningContentFolders.AtelierDraftSkill, "samples", "diversification.json"),
            Path.Combine(folder.Path, "terms", "diversification.json"));

        AssertNoProblems(LearningContentChecker.Check(folder.Path, Rules));
    }

    [Fact]
    public void AnEmptyContentFolder_PassesEveryCheck()
    {
        using LearningContentFolders.TempFolder folder = LearningContentFolders.Empty();

        AssertNoProblems(LearningContentChecker.Check(folder.Path, Rules));
    }

    [Fact]
    public void TheFixtureContent_PassesEveryCheck()
    {
        AssertNoProblems(LearningContentChecker.Check(LearningContentFolders.Fixture, Rules));
    }

    [Theory]
    [MemberData(nameof(MutationNames))]
    public void EachCheck_RejectsABrokenFile_NamingTheFileAndField(string name)
    {
        Mutation mutation = Mutations[name];
        using LearningContentFolders.TempFolder folder = LearningContentFolders.CopyOfFixture();
        mutation.ApplyTo(folder.Path);

        IReadOnlyList<ContentProblem> problems = LearningContentChecker.Check(folder.Path, Rules);

        Assert.True(problems.Any(p => p.File == mutation.File && p.Field == mutation.Field),
            $"Expected a problem in {mutation.File} at {mutation.Field}, got:\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public void TheAllowlist_RowsAreWellFormed()
    {
        Assert.NotEmpty(Rules.Sources);
        Assert.All(Rules.Sources, source =>
        {
            Assert.Contains(source.Usage, ApprovedSource.Usages);
            Assert.NotEmpty(source.UrlPrefixes);
            // An https prefix ending in "/" so "https://www.irs.gov.example.com" never passes as the IRS.
            Assert.All(source.UrlPrefixes, prefix =>
                Assert.True(prefix.StartsWith("https://", StringComparison.Ordinal) && prefix.EndsWith('/'),
                    $"{source.Name}: {prefix} is not an https prefix ending in /"));
        });
    }

    [Fact]
    public void ThePlainEnglishLists_AreNotEmpty()
    {
        Assert.NotEmpty(Rules.AdvicePhrases);
        Assert.NotEmpty(Rules.Idioms);
    }

    private static void AssertNoProblems(IReadOnlyList<ContentProblem> problems) =>
        Assert.True(problems.Count == 0, "Content problems:\n  " + string.Join("\n  ", problems));

    /// <summary>One break to a fixture file, and the field the checks must name for it.</summary>
    private sealed record Mutation(string File, string Field, Action<JsonObject> Edit)
    {
        // A file the fixture does not have starts as an empty object.
        public void ApplyTo(string folder)
        {
            string path = Path.Combine(folder, File);
            JsonObject root = System.IO.File.Exists(path)
                ? JsonNode.Parse(System.IO.File.ReadAllText(path))!.AsObject()
                : [];
            Edit(root);
            System.IO.File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
