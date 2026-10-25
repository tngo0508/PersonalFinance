using System.Globalization;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// Paying off a debt by simulating monthly payments (#303). Golden values were simulated independently at 50-digit
/// precision.
/// </summary>
public class DebtPayoffTests
{
    [Fact]
    public void Simulate_MatchesTheGoldenValue_For5000At22PercentPaying500AMonth()
    {
        DebtPayoffResult result = DebtPayoff.Simulate(5_000m, 22m, 500m);

        Assert.Equal(DebtPayoffOutcome.PaidOff, result.Outcome);
        Assert.Equal(12, result.Months);
        Assert.Equal(574.44m, decimal.Round(result.TotalInterest!.Value, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(74.44m, decimal.Round(result.FinalPayment!.Value, 2, MidpointRounding.AwayFromZero));
        Assert.True(Math.Abs(result.TotalInterest.Value - 574.439854872883792214302677682m) < 0.000_000_01m);
        Assert.True(Math.Abs(result.FinalPayment.Value - 74.439854872883792214302677682m) < 0.000_000_01m);
    }

    [Theory]
    [InlineData("100")]
    [InlineData("99.99")]
    [InlineData("1")]
    public void Simulate_WhenTheAmountIsAtOrBelowAMonthsInterest_SaysSoInsteadOfFigures(string payment)
    {
        // 5,000 at 24% accrues exactly 100 in the first month.
        DebtPayoffResult result = DebtPayoff.Simulate(5_000m, 24m, Parse(payment));

        Assert.Equal(DebtPayoffOutcome.AmountDoesNotCoverInterest, result.Outcome);
        Assert.Equal(100m, result.FirstMonthInterest);
        Assert.Null(result.Months);
        Assert.Null(result.TotalInterest);
        Assert.Null(result.FinalPayment);
    }

    [Fact]
    public void Simulate_WhenTheAmountIsACentAboveAMonthsInterest_PaysOff()
    {
        DebtPayoffResult result = DebtPayoff.Simulate(5_000m, 24m, 100.01m);

        Assert.Equal(DebtPayoffOutcome.PaidOff, result.Outcome);
        Assert.InRange(result.Months!.Value, 400, DebtPayoff.MaxMonths);
    }

    [Fact]
    public void Simulate_AtZeroApr_ChargesNoInterest_AndCountsWholePayments()
    {
        DebtPayoffResult result = DebtPayoff.Simulate(5_000m, 0m, 300m);

        Assert.Equal(DebtPayoffOutcome.PaidOff, result.Outcome);
        Assert.Equal(0m, result.FirstMonthInterest);
        Assert.Equal(17, result.Months);
        Assert.Equal(0m, result.TotalInterest);
        Assert.Equal(200m, result.FinalPayment);
    }

    [Fact]
    public void Simulate_WhenItTakesLongerThan50Years_SaysSoInsteadOfFigures()
    {
        // 1,000,000 at 7% accrues 5,833.33 a month; 5,834 a month needs about 130 years.
        DebtPayoffResult result = DebtPayoff.Simulate(1_000_000m, 7m, 5_834m);

        Assert.Equal(DebtPayoffOutcome.LongerThanMaximum, result.Outcome);
        Assert.Null(result.Months);
        Assert.Null(result.TotalInterest);
        Assert.Null(result.FinalPayment);
    }

    [Fact]
    public void Simulate_WithNothingOwed_IsPaidOffInZeroMonths()
    {
        DebtPayoffResult result = DebtPayoff.Simulate(0m, 22m, 500m);

        Assert.Equal(DebtPayoffOutcome.PaidOff, result.Outcome);
        Assert.Equal(0, result.Months);
        Assert.Equal(0m, result.TotalInterest);
    }

    [Theory]
    [InlineData("5000", "22", "500")]
    [InlineData("5000", "0", "300")]
    [InlineData("5000", "0", "5000")]
    [InlineData("5000", "0", "7000")]
    [InlineData("12345.67", "29.99", "400")]
    [InlineData("800", "7", "799.99")]
    [InlineData("50000", "100", "4200")]
    [InlineData("1000000", "7", "1000000")]
    public void Simulate_TakesAtLeastBalanceOverAmountMonths_AndNeverEndsWithAnOverpayment(
        string balance, string apr, string payment)
    {
        decimal owed = Parse(balance);
        decimal amount = Parse(payment);

        DebtPayoffResult result = DebtPayoff.Simulate(owed, Parse(apr), amount);

        Assert.Equal(DebtPayoffOutcome.PaidOff, result.Outcome);
        int months = result.Months!.Value;
        Assert.True(months >= Math.Ceiling(owed / amount), $"{months} months is fewer than {owed} / {amount}.");
        decimal finalPayment = result.FinalPayment!.Value;
        Assert.InRange(finalPayment, 0.000_000_01m, amount);
        // Every payment but the last is the whole amount; together they pay the balance and the interest, no more.
        decimal paid = (months - 1) * amount + finalPayment;
        Assert.True(Math.Abs(paid - (owed + result.TotalInterest!.Value)) < 0.000_000_01m,
            $"Paid {paid}, owed {owed} plus {result.TotalInterest} interest.");
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
