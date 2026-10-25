using System.Globalization;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// Month-by-month growth of a monthly contribution (#303). Golden values are the closed-form annuity formula evaluated
/// at 50-digit precision, not derived from the code.
/// </summary>
public class CompoundGrowthTests
{
    [Fact]
    public void BalanceAfter_MatchesTheGoldenValue_For500AMonthFor10YearsAt7Percent()
    {
        // 500 * ((1 + 0.07/12)^120 - 1) / (0.07/12) at 50 digits.
        const decimal closedForm = 86_542.403716768050298217695338655998884m;

        decimal balance = CompoundGrowth.BalanceAfter(500m, 7m, 120);

        Assert.Equal(86_542.40m, decimal.Round(balance, 2, MidpointRounding.AwayFromZero));
        Assert.True(Math.Abs(balance - closedForm) < 0.000_000_01m, $"Balance {balance} drifted from {closedForm}.");
    }

    [Theory]
    [InlineData(500, 10, 600, "8662195.3979837823173871424742")]
    [InlineData(500, 4, 12, "6111.2314379684455778099385259")]
    [InlineData(1_000_000, 10, 600, "17324390795.967564634774284948")]
    public void BalanceAfter_MatchesTheClosedForm_AtTheShortestAndLongestHorizons(
        int monthly, int ratePercent, int months, string closedForm)
    {
        decimal expected = decimal.Parse(closedForm, CultureInfo.InvariantCulture);

        decimal balance = CompoundGrowth.BalanceAfter(monthly, ratePercent, months);

        Assert.True(Math.Abs(balance - expected) < 0.000_01m, $"Balance {balance} drifted from {expected}.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(600)]
    public void BalanceAfter_AtZeroPercent_IsTheSumOfContributions(int months)
    {
        decimal balance = CompoundGrowth.BalanceAfter(123.45m, 0m, months);

        Assert.Equal(123.45m * months, balance);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3.5")]
    [InlineData("4")]
    [InlineData("7")]
    [InlineData("10")]
    public void BalanceAfter_NeverDecreases_FromOneMonthToTheNext(string ratePercent)
    {
        decimal rate = decimal.Parse(ratePercent, CultureInfo.InvariantCulture);
        decimal previous = 0m;
        for (int months = 1; months <= 600; months++)
        {
            decimal balance = CompoundGrowth.BalanceAfter(0.01m, rate, months);
            Assert.True(balance > previous, $"Month {months} at {rate}%: {balance} is not above {previous}.");
            previous = balance;
        }
    }

    [Fact]
    public void BalanceAfter_NoMonths_IsZero()
    {
        Assert.Equal(0m, CompoundGrowth.BalanceAfter(500m, 7m, 0));
    }
}
