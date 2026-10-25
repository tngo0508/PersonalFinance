using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>L1: paying the whole monthly amount toward the debt.</summary>
internal sealed record DebtPayoffFigures
{
    /// <summary>The debt balance the user entered.</summary>
    public required decimal Balance { get; init; }

    /// <summary>The debt's APR in percent: a guaranteed return when the debt is paid off.</summary>
    public required decimal AprPercent { get; init; }

    /// <summary>The assumed investment return the APR is compared against (<see cref="PriorityAssumptions.BaseReturnPercent"/>).</summary>
    public required decimal AssumedReturnPercent { get; init; }

    /// <summary>The simulated payoff. When it is not <see cref="DebtPayoffOutcome.PaidOff"/>, the page shows no figures.</summary>
    public required DebtPayoffResult Payoff { get; init; }
}

/// <summary>L2: closing the gap to a 3-month emergency fund with the monthly amount, with no growth.</summary>
internal sealed record EmergencyFundFigures
{
    /// <summary>The months of expenses the fund should cover (3).</summary>
    public required decimal TargetMonths { get; init; }

    /// <summary>The months of expenses the fund covers today.</summary>
    public required decimal MonthsCovered { get; init; }

    /// <summary>The monthly expenses the user entered.</summary>
    public required decimal MonthlyExpenses { get; init; }

    /// <summary>Target months × monthly expenses.</summary>
    public required decimal TargetAmount { get; init; }

    /// <summary>Target months × expenses − months covered × expenses, never below zero.</summary>
    public required decimal Gap { get; init; }

    /// <summary>Months of the monthly amount to close the gap, rounded up; 0 when the fund is at its target.</summary>
    public required int MonthsToClose { get; init; }
}

/// <summary>L3: the match captured by contributing the monthly amount.</summary>
internal sealed record EmployerMatchFigures
{
    /// <summary>The employer match in percent, which is also an instant return on each matched dollar.</summary>
    public required decimal MatchPercent { get; init; }

    /// <summary>"Up to" this much match a year: 12 × the monthly amount × the match percent. The employer's cap may be lower.</summary>
    public required decimal YearlyMatch { get; init; }

    /// <summary>The assumed investment return the match percent is compared against.</summary>
    public required decimal AssumedReturnPercent { get; init; }
}

/// <summary>L6: the yearly amount fills the IRA limit, then the 401(k) limit, then a taxable account.</summary>
internal sealed record RetirementSplitFigures
{
    /// <summary>The tax year of the IRS limits used. The page shows it, so a fallback year is visible.</summary>
    public required int TaxYear { get; init; }

    /// <summary>The age range that sets the catch-up.</summary>
    public required RetirementAgeBand AgeBand { get; init; }

    /// <summary>12 × the monthly amount.</summary>
    public required decimal YearlyAmount { get; init; }

    /// <summary>The IRA limit for the age range, catch-up included.</summary>
    public required decimal IraLimit { get; init; }

    /// <summary>The 401(k) employee deferral limit for the age range, catch-up included.</summary>
    public required decimal EmployeeDeferralLimit { get; init; }

    /// <summary>The part of the yearly amount that goes to the IRA.</summary>
    public required decimal ToIra { get; init; }

    /// <summary>The part that goes to the 401(k).</summary>
    public required decimal ToEmployeePlan { get; init; }

    /// <summary>The part above both limits, which goes to a taxable account.</summary>
    public required decimal ToTaxable { get; init; }
}

/// <summary>The balance at the horizon under one scenario.</summary>
/// <param name="Scenario">The scenario.</param>
/// <param name="RatePercent">Its yearly rate in percent.</param>
/// <param name="Balance">The balance at the horizon, full precision.</param>
internal sealed record ScenarioBalance(GrowthScenario Scenario, decimal RatePercent, decimal Balance);
