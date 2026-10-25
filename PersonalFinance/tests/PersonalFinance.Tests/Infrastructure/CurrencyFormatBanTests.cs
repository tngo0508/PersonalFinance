using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The <c>"C"</c> / <c>"C0"</c> currency format takes its symbol from the culture, so a vi-VN reader would see a USD
/// amount as đồng (ADR 0016, localization spec §2.2). Money goes through <c>MoneyFormatter</c>, the only place allowed
/// to use it.
/// </summary>
public class CurrencyFormatBanTests
{
    private static readonly string[] Projects = ["PersonalFinance.Web", "PersonalFinance.ApiService", "PersonalFinance.Shared"];

    private static readonly Regex CurrencyFormat = new(
        @"ToString\(\s*""[Cc]\d*""|\{[^{}""]+:[Cc]\d*\}|\.Format\([^)]*""[^""]*\{\d+:[Cc]\d*\}",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    [Fact]
    public void NoSourceFile_UsesTheCultureCurrencyFormat()
    {
        List<string> offenders = Projects
            .SelectMany(project => SourceFiles(RepositoryRoot.SourceProject(project)))
            .Where(path => Path.GetFileName(path) != "MoneyFormatter.cs")
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, number: index + 1)))
            .Where(entry => CurrencyFormat.IsMatch(entry.line))
            .Select(entry => $"{Path.GetRelativePath(RepositoryRoot.Find(), entry.path)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Use MoneyFormatter.Format / FormatWhole instead of a \"C\" format:\n" + string.Join("\n", offenders));
    }

    [Theory]
    [InlineData("@goal.CurrentAmount.ToString(\"C0\")")]
    [InlineData("value.ToString(\"C\", CultureInfo.CurrentCulture)")]
    [InlineData("$\"Target: {g.TargetAmount:C}\"")]
    [InlineData("$\"reaches {last.Base:C0} by\"")]
    [InlineData("string.Format(culture, \"{0:C2}\", amount)")]
    public void ThePattern_CatchesEveryCurrencyFormatShape(string line)
    {
        Assert.Matches(CurrencyFormat, line);
    }

    [Theory]
    [InlineData("amount.ToString(\"N0\", culture)")]
    [InlineData("$\"{t.Date:MMM d, yyyy}\"")]
    [InlineData("Guid.NewGuid().ToString(\"N\")")]
    [InlineData("MoneyFormatter.FormatWhole(goal.CurrentAmount, SupportedCurrencies.Default.Code)")]
    public void ThePattern_LeavesOtherFormatsAlone(string line)
    {
        Assert.DoesNotMatch(CurrencyFormat, line);
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".cshtml", StringComparison.Ordinal))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin" or "Migrations"));
}
