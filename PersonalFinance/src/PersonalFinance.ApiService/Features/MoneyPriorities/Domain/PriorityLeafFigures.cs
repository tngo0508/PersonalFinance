namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The dollar figures for the leaf a path ends at, from <see cref="PriorityLeafCalculator.Calculate"/>. Exactly the
/// sections the leaf shows are set; the rest are null or empty. Every amount is full precision: the API sends it as is
/// and the page rounds once, at display (spec #315, "Display").
/// </summary>
internal sealed record PriorityLeafFigures
{
    /// <summary>The leaf the figures are for, e.g. <c>L5</c>.</summary>
    public required string LeafId { get; init; }

    /// <summary>L1: the payoff of the high-interest debt.</summary>
    public DebtPayoffFigures? Debt { get; init; }

    /// <summary>L2: the gap to a 3-month emergency fund.</summary>
    public EmergencyFundFigures? EmergencyFund { get; init; }

    /// <summary>L3: the employer match being left behind.</summary>
    public EmployerMatchFigures? EmployerMatch { get; init; }

    /// <summary>L6: how the yearly amount splits between the IRA, the 401(k) and a taxable account.</summary>
    public RetirementSplitFigures? RetirementSplit { get; init; }

    /// <summary>
    /// L4 to L7: the balance at the horizon under each scenario, lowest rate first. L4 shows Cash and Conservative;
    /// L5, L6 and L7 show Conservative, Base and Aggressive. Empty for L1 to L3.
    /// </summary>
    public IReadOnlyList<ScenarioBalance> Scenarios { get; init; } = [];

    /// <summary>L4 to L7: the whole years the balances are projected over.</summary>
    public int? HorizonYears { get; init; }

    /// <summary>L4 to L7: the sum of the monthly contributions over the horizon, before any growth.</summary>
    public decimal? TotalContributed { get; init; }
}
