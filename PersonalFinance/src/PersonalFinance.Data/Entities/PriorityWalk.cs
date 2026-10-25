namespace PersonalFinance.Data.Entities;

/// <summary>
/// A Priority Walk: one saved pass through the Priority Tree on Money Priorities, holding an Account's answers, the
/// path they lead to and the priority (leaf) at its end. A null answer means the question was not asked on this path.
/// Dollar figures are never stored; they are recomputed from the answers whenever the Walk is shown.
/// </summary>
public class PriorityWalk : AuditableEntity
{
    /// <summary>The extra amount the user has each month, in dollars (over 0, at most 1,000,000).</summary>
    public decimal MonthlyAmount { get; set; }

    /// <summary>What the user wants their money to do, in their own words (at most 300 characters); optional.</summary>
    public string? Purpose { get; set; }

    /// <summary>The APR of the user's highest-interest debt, in percent (0 to 100); 0 means no debt.</summary>
    public decimal DebtApr { get; set; }

    /// <summary>The debt balance in dollars; asked only when the APR is 7% or more.</summary>
    public decimal? DebtBalance { get; set; }

    /// <summary>How many months of expenses the emergency fund covers (0 to 120).</summary>
    public decimal? EmergencyFundMonths { get; set; }

    /// <summary>Monthly expenses in dollars; asked only when the emergency fund covers less than 3 months.</summary>
    public decimal? MonthlyExpenses { get; set; }

    /// <summary>Whether the user already contributes enough to get the full employer match.</summary>
    public bool? HasFullEmployerMatch { get; set; }

    /// <summary>The employer match, in percent of pay (0 to 100); asked only when the match is not full.</summary>
    public decimal? EmployerMatchPercent { get; set; }

    /// <summary>When the user needs the money, in whole years (1 to 50).</summary>
    public int? HorizonYears { get; set; }

    /// <summary>The age range for IRS catch-up contributions, stored as the age band's enum name.</summary>
    public string? AgeBand { get; set; }

    /// <summary>Whether the user already contributes the IRS limit for their age.</summary>
    public bool? AtContributionLimit { get; set; }

    /// <summary>The version of the Priority Tree the Walk was evaluated against.</summary>
    public int TreeVersion { get; set; }

    /// <summary>The tax year of the IRS limits used, when the path reached the limit question.</summary>
    public int? LimitsTaxYear { get; set; }

    /// <summary>The ordered node ids of the path, comma separated, e.g. <c>S,Q1,Q2,Q3,Q4,L5</c>.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The id of the leaf the path ends at, e.g. <c>L5</c>.</summary>
    public string LeafId { get; set; } = string.Empty;

    /// <summary>The AI-written Purpose Note saved onto the Walk after it is shown; null until written.</summary>
    public string? PurposeNote { get; set; }
}
