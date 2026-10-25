namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The balance a monthly contribution grows to (#303). Contributions arrive at month end and compound monthly at a
/// nominal yearly rate. Every value is <see cref="decimal"/>: <see cref="Math.Pow(double, double)"/> takes only
/// <see cref="double"/>, so growth is stepped month by month (balance × (1 + rate ÷ 12) + contribution). Nothing is
/// rounded; rounding happens once, at display.
/// </summary>
internal static class CompoundGrowth
{
    /// <summary>
    /// The balance after <paramref name="months"/> month-end contributions of <paramref name="monthlyContribution"/>,
    /// starting from nothing, at <paramref name="yearlyRatePercent"/> (7 is 7%) compounded monthly.
    /// </summary>
    public static decimal BalanceAfter(decimal monthlyContribution, decimal yearlyRatePercent, int months)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyContribution);
        ArgumentOutOfRangeException.ThrowIfNegative(yearlyRatePercent);
        ArgumentOutOfRangeException.ThrowIfNegative(months);

        decimal balance = 0m;
        for (int month = 0; month < months; month++)
        {
            balance += balance * yearlyRatePercent / 1200m + monthlyContribution;
        }

        return balance;
    }
}
