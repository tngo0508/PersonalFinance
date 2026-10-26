using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Legal;

/// <summary>
/// The Landing Page, About and Legal Pages in every supported language (spec §1, §3 L3). Legal text stays English:
/// each Legal Page keeps its body tagged <c>lang="en"</c> and shows the translated "legal text is provided in English"
/// notice; everything else on these pages is a localizer key that English and Vietnamese both define.
/// </summary>
public partial class LegalPagesLanguageTests
{
    private static readonly string ViewsRoot = Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), "Views");

    public static TheoryData<string> LegalPages()
    {
        var pages = new TheoryData<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(ViewsRoot, "Legal"), "*.cshtml")
                     .Where(f => !Path.GetFileName(f).StartsWith('_')))
        {
            pages.Add(Path.GetFileName(file));
        }

        return pages;
    }

    [Theory]
    [MemberData(nameof(LegalPages))]
    public void LegalPage_ShowsTheEnglishOnlyNotice_AndTagsItsTextAsEnglish(string page)
    {
        string source = File.ReadAllText(Path.Combine(ViewsRoot, "Legal", page));

        Assert.Contains("<partial name=\"_EnglishOnlyNotice\"", source, StringComparison.Ordinal);
        Assert.Contains("lang=\"en\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EnglishOnlyNotice_IsALocalizedString()
    {
        string source = File.ReadAllText(Path.Combine(ViewsRoot, "Legal", "_EnglishOnlyNotice.cshtml"));

        Assert.Contains("L[\"Legal.EnglishOnlyNotice\"]", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Home")]
    [InlineData("Legal")]
    public void EveryKeyTheViewsUse_HasEnglishAndVietnameseText(string folder)
    {
        List<string> keys = Directory.GetFiles(Path.Combine(ViewsRoot, folder), "*.cshtml")
            .SelectMany(file => KeyReference().Matches(File.ReadAllText(file)).Select(m => m.Groups["key"].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key =>
        {
            Assert.True(TestLocalization.Catalog.TryGet("en", key, out _), $"en.json has no {key}.");
            Assert.True(TestLocalization.Catalog.TryGet("vi", key, out _), $"vi.json has no {key}.");
        });
    }

    // L["Key"], L.Named("Key", ...) and L.NamedHtml("Key", ...).
    [GeneratedRegex(@"\bL(?:\[|\.Named\(|\.NamedHtml\()\s*""(?<key>[A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)""")]
    private static partial Regex KeyReference();
}
