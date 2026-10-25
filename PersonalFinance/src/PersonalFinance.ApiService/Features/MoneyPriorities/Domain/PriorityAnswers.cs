using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// What the user entered for one Priority Walk, before the path is known. Null means "not given". The path evaluator
/// reads only the answers its path asks for; <see cref="PriorityPath.Answers"/> is the same set with the answers off
/// the path cleared to null, which is what a Priority Walk stores. Rates are percentages (22.99 is 22.99%).
/// The Purpose is not an answer: it never changes the path or the figures.
/// </summary>
internal sealed record PriorityAnswers
{
    /// <summary>Extra money each month, in dollars. Always asked.</summary>
    public required decimal MonthlyAmount { get; init; }

    /// <summary>Q1: the highest debt APR in percent; 0 means no debt. Always asked.</summary>
    public required decimal DebtApr { get; init; }

    /// <summary>Follow-up to Q1 Yes (L1): the balance of that debt, in dollars.</summary>
    public decimal? DebtBalance { get; init; }

    /// <summary>Q2: how many months of expenses the emergency fund covers. Asked when Q1 is No.</summary>
    public decimal? EmergencyFundMonths { get; init; }

    /// <summary>Follow-up to Q2 No (L2): monthly expenses, in dollars.</summary>
    public decimal? MonthlyExpenses { get; init; }

    /// <summary>Q3: getting the full employer match. Asked when Q2 is Yes.</summary>
    public bool? HasFullEmployerMatch { get; init; }

    /// <summary>Follow-up to Q3 No (L3): the employer match in percent of pay.</summary>
    public decimal? EmployerMatchPercent { get; init; }

    /// <summary>Q4: years until the money is needed. Asked when Q3 is Yes.</summary>
    public int? HorizonYears { get; init; }

    /// <summary>Asked with Q5 when the horizon is over 10 years: the age range that sets the catch-up.</summary>
    public RetirementAgeBand? AgeBand { get; init; }

    /// <summary>Q5: already at the IRS limit for the age range. Asked when the horizon is over 10 years.</summary>
    public bool? AtContributionLimit { get; init; }
}
