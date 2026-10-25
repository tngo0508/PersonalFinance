using System.Globalization;
using System.Text.RegularExpressions;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The accuracy rules of the spec (#315, "Testing Decisions"): decimal-only math, one rounding at display, and the
/// estimate disclaimer under every result.
/// </summary>
public partial class LeafMathAccuracyTests
{
    [Fact]
    public void Domain_NeverUsesDoubleFloatPowOrLog()
    {
        string domain = Path.Combine(
            RepositoryRoot.SourceProject("PersonalFinance.ApiService"), "Features", "MoneyPriorities", "Domain");
        string[] files = Directory.GetFiles(domain, "*.cs");
        Assert.NotEmpty(files);

        var offences = files
            .SelectMany(file => File.ReadAllLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(l => !l.line.TrimStart().StartsWith("///", StringComparison.Ordinal))
            .Where(l => ForbiddenMath().IsMatch(l.line))
            .Select(l => $"{Path.GetFileName(l.file)}:{l.number}: {l.line.Trim()}")
            .ToList();

        Assert.True(offences.Count == 0, "Money Priorities math must be decimal only:\n" + string.Join('\n', offences));
    }

    [Theory]
    [InlineData("86542.40", "86542")]
    [InlineData("86542.5", "86543")]
    [InlineData("86541.5", "86542")]
    [InlineData("0.4999", "0")]
    [InlineData("-2.5", "-3")]
    [InlineData("574.43985487288379221430267768", "574")]
    public void WholeDollars_RoundsMidpointAwayFromZero(string value, string expected)
    {
        decimal parsed = decimal.Parse(value, CultureInfo.InvariantCulture);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), DisplayRounding.WholeDollars(parsed));
    }

    [Fact]
    public void EstimateDisclaimer_IsTheSpecsWording_AndNotTheAiDisclaimer()
    {
        Assert.Equal(
            "Estimates assume a constant yearly return and monthly contributions. They leave out taxes, fees and inflation. "
            + "This is not financial advice.",
            MoneyPrioritiesMessages.EstimateDisclaimer);
        Assert.NotEqual(AiAssistantMessages.Disclaimer, MoneyPrioritiesMessages.EstimateDisclaimer);
    }

    [GeneratedRegex(@"\b(double|float|Single|Math\.Pow|Math\.Log\w*|MathF)\b")]
    private static partial Regex ForbiddenMath();
}
