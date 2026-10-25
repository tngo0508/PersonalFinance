namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The result of <see cref="DebtPayoff.Simulate"/>. Full precision: nothing is rounded. <see cref="Months"/>,
/// <see cref="TotalInterest"/> and <see cref="FinalPayment"/> are set only when <see cref="Outcome"/> is
/// <see cref="DebtPayoffOutcome.PaidOff"/>, so no figure is ever shown for a debt that is not paid off.
/// </summary>
internal sealed record DebtPayoffResult
{
    /// <summary>How the simulation ended.</summary>
    public required DebtPayoffOutcome Outcome { get; init; }

    /// <summary>The interest the debt accrues in its first month: the amount must be above this to make progress.</summary>
    public required decimal FirstMonthInterest { get; init; }

    /// <summary>Months of payments until the debt is cleared, the final (partial) month included.</summary>
    public int? Months { get; init; }

    /// <summary>All the interest paid over those months.</summary>
    public decimal? TotalInterest { get; init; }

    /// <summary>The last payment, capped at what was owed, so a payoff never ends with an overpayment.</summary>
    public decimal? FinalPayment { get; init; }
}
