namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// Paying a debt off with a fixed monthly amount (#303). Each month the balance accrues a twelfth of the APR, then the
/// payment is taken; the final payment is capped at what is owed. Months are counted by simulating payments, because
/// <see cref="Math.Log(double)"/> takes only <see cref="double"/>. Every value is <see cref="decimal"/> and nothing is
/// rounded; rounding happens once, at display.
/// </summary>
internal static class DebtPayoff
{
    /// <summary>The longest payoff simulated: 50 years, the longest horizon the page asks about.</summary>
    public const int MaxMonths = 600;

    /// <summary>
    /// Simulates paying <paramref name="monthlyPayment"/> a month against <paramref name="balance"/> at
    /// <paramref name="aprPercent"/> (22 is 22%).
    /// </summary>
    public static DebtPayoffResult Simulate(decimal balance, decimal aprPercent, decimal monthlyPayment)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(balance);
        ArgumentOutOfRangeException.ThrowIfNegative(aprPercent);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyPayment);

        decimal firstMonthInterest = MonthInterest(balance, aprPercent);
        if (balance > 0m && monthlyPayment <= firstMonthInterest)
        {
            return new DebtPayoffResult
            {
                Outcome = DebtPayoffOutcome.AmountDoesNotCoverInterest,
                FirstMonthInterest = firstMonthInterest
            };
        }

        decimal owed = balance;
        decimal totalInterest = 0m;
        decimal payment = 0m;
        int months = 0;
        while (owed > 0m)
        {
            if (months == MaxMonths)
            {
                return new DebtPayoffResult
                {
                    Outcome = DebtPayoffOutcome.LongerThanMaximum,
                    FirstMonthInterest = firstMonthInterest
                };
            }

            decimal interest = MonthInterest(owed, aprPercent);
            totalInterest += interest;
            owed += interest;
            payment = Math.Min(monthlyPayment, owed);
            owed -= payment;
            months++;
        }

        return new DebtPayoffResult
        {
            Outcome = DebtPayoffOutcome.PaidOff,
            FirstMonthInterest = firstMonthInterest,
            Months = months,
            TotalInterest = totalInterest,
            FinalPayment = payment
        };
    }

    /// <summary>
    /// A month's interest: a twelfth of the APR. Multiplying before dividing keeps whole-cent cases exact (30,000 at 20%
    /// is exactly 500), so an amount equal to a month's interest is never mistaken for one just above it.
    /// </summary>
    private static decimal MonthInterest(decimal owed, decimal aprPercent) => owed * aprPercent / 1200m;
}
