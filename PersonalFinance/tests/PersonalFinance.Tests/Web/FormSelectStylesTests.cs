using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// The global select styling in site.css must keep Bootstrap's room for the chevron on the right.
/// site.css loads after every page's own stylesheet, so a padding shorthand there overrides both
/// Bootstrap's 2.25rem and any equal-specificity page rule, and the selected text runs under the chevron.
/// </summary>
public class FormSelectStylesTests
{
    private static readonly string SiteCss = Regex.Replace(
        File.ReadAllText(Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), "wwwroot", "css", "site.css")),
        @"/\*.*?\*/",
        string.Empty,
        RegexOptions.Singleline);

    [Fact]
    public void SiteCss_FormSelect_KeepsRoomForTheChevron()
    {
        // The last padding declaration site.css applies to .form-select decides the right padding.
        List<string> paddingDeclarations = new List<string>();
        foreach (Match rule in Regex.Matches(SiteCss, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}"))
        {
            bool targetsSelect = rule.Groups["selectors"].Value
                .Split(',')
                .Select(selector => selector.Trim())
                .Any(selector => selector == ".form-select");
            if (!targetsSelect)
            {
                continue;
            }

            foreach (Match declaration in Regex.Matches(rule.Groups["body"].Value, @"(?<name>padding(-right)?)\s*:\s*(?<value>[^;]+);"))
            {
                paddingDeclarations.Add($"{declaration.Groups["name"].Value}:{declaration.Groups["value"].Value.Trim()}");
            }
        }

        Assert.NotEmpty(paddingDeclarations);
        string last = paddingDeclarations[^1];
        Assert.True(last.StartsWith("padding-right:", StringComparison.Ordinal),
            $"site.css ends its .form-select padding with '{last}'; a shorthand there drops the room Bootstrap reserves for the chevron.");
        Assert.Equal("padding-right:2.25rem", last);
    }
}
