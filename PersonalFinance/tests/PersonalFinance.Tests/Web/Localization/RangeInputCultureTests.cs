using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// A range slider's value is read by the browser in the invariant format only. Under vi-VN, <c>value="@Model.BaseRoi"</c>
/// renders <c>7,5</c>, which the browser drops, and the slider jumps to its midpoint (spec §2.2, slice L2).
/// </summary>
public class RangeInputCultureTests
{
    private static readonly Regex RangeWithRazorValue = new(@"type=""range""[^>]*value=""@(?<expr>[^""]*)""",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    [Fact]
    public void RangeInputs_RenderTheirRazorValueInTheInvariantCulture()
    {
        string web = RepositoryRoot.SourceProject("PersonalFinance.Web");
        List<string> offenders = Directory.EnumerateFiles(web, "*.cshtml", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .SelectMany(path => RangeWithRazorValue.Matches(File.ReadAllText(path))
                .Where(match => !match.Groups["expr"].Value.Contains("InvariantCulture", StringComparison.Ordinal)
                                && !match.Groups["expr"].Value.Contains("Count", StringComparison.Ordinal))
                .Select(match => $"{Path.GetFileName(path)}: {match.Value}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Use .ToString(CultureInfo.InvariantCulture):\n" + string.Join("\n", offenders));
    }
}
