namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>How a simulated debt payoff ends.</summary>
internal enum DebtPayoffOutcome
{
    /// <summary>The payments clear the debt; months, interest and the final payment are known.</summary>
    PaidOff,

    /// <summary>
    /// The monthly amount is at or below the first month's interest, so the debt never shrinks. The page says so
    /// instead of showing figures.
    /// </summary>
    AmountDoesNotCoverInterest,

    /// <summary>
    /// The debt shrinks, but not within <see cref="DebtPayoff.MaxMonths"/> months. The page says it takes longer than
    /// that instead of showing figures.
    /// </summary>
    LongerThanMaximum
}
