using System.Text.Json.Serialization;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.MoneyPriorities;

/// <summary>
/// The answers a user submits for one Priority Walk. A follow-up is read only when its question is on the path the
/// answers lead to; off the path it is ignored (not validated, stored as null). Rates are percentages (22.99 is
/// 22.99%); money is in dollars.
/// </summary>
public class SubmitPriorityWalkRequest
{
    /// <summary>The extra amount each month: over 0, at most 1,000,000. Required.</summary>
    public decimal? MonthlyAmount { get; set; }

    /// <summary>What the user wants the money to do, in their own words; optional, at most 300 characters.</summary>
    public string? Purpose { get; set; }

    /// <summary>Q1: the highest debt APR, 0 to 100; 0 means no debt. Required.</summary>
    public decimal? DebtApr { get; set; }

    /// <summary>Required when the APR is 7% or more: the balance of that debt.</summary>
    public decimal? DebtBalance { get; set; }

    /// <summary>Q2, asked when the APR is under 7%: months of expenses the emergency fund covers, 0 to 120.</summary>
    public decimal? EmergencyFundMonths { get; set; }

    /// <summary>Required when the fund covers under 3 months: monthly expenses.</summary>
    public decimal? MonthlyExpenses { get; set; }

    /// <summary>Q3, asked when the fund covers 3 months or more: getting the full employer match.</summary>
    public bool? HasFullEmployerMatch { get; set; }

    /// <summary>Required when the match is not full: the employer match in percent of pay, 0 to 100.</summary>
    public decimal? EmployerMatchPercent { get; set; }

    /// <summary>Q4, asked when the match is full: years until the money is needed, 1 to 50.</summary>
    public int? HorizonYears { get; set; }

    /// <summary>
    /// Required when the horizon is over 10 years: the age range that sets the IRS catch-up. Always a number on the
    /// wire (1, 2, 3); the attribute overrides Refit's default string enums, which the ApiService cannot bind.
    /// </summary>
    [JsonConverter(typeof(JsonNumberEnumConverter<RetirementAgeBand>))]
    public RetirementAgeBand? AgeBand { get; set; }

    /// <summary>Q5, asked when the horizon is over 10 years: already at the IRS limit for the age range.</summary>
    public bool? AtContributionLimit { get; set; }
}

/// <summary>
/// The answers a Priority Walk stored, with every answer off its path null; the page refills its form from them.
/// </summary>
public class PriorityWalkAnswersDto
{
    public decimal MonthlyAmount { get; set; }
    public string? Purpose { get; set; }
    public decimal DebtApr { get; set; }
    public decimal? DebtBalance { get; set; }
    public decimal? EmergencyFundMonths { get; set; }
    public decimal? MonthlyExpenses { get; set; }
    public bool? HasFullEmployerMatch { get; set; }
    public decimal? EmployerMatchPercent { get; set; }
    public int? HorizonYears { get; set; }
    public RetirementAgeBand? AgeBand { get; set; }
    public bool? AtContributionLimit { get; set; }
}

/// <summary>
/// A Priority Walk as the result card shows it: the suggested priority, its figures recomputed from the stored answers,
/// the path in words, both diagram texts and the stated assumptions. Every figure is full precision; the page rounds
/// once, at display.
/// </summary>
public class PriorityWalkResultDto
{
    /// <summary>The Priority Walk's id, for Earlier results and the Purpose Note.</summary>
    public int WalkId { get; set; }

    /// <summary>When the Walk was saved (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The Priority Tree version the Walk was answered against.</summary>
    public int TreeVersion { get; set; }

    /// <summary>The leaf the path ends at, e.g. <c>L1</c>.</summary>
    public string LeafId { get; set; } = string.Empty;

    /// <summary>The leaf's label, e.g. "Pay down the high-interest debt first".</summary>
    public string LeafLabel { get; set; } = string.Empty;

    /// <summary>
    /// The leaf's label as the end of the headline sentence, e.g. "Pay down the high-interest debt first.", for
    /// "Put your $500.00 a month toward: ...".
    /// </summary>
    public string HeadlineLeafLabel { get; set; } = string.Empty;

    /// <summary>The extra monthly amount the user entered, with its cents.</summary>
    public decimal MonthlyAmount { get; set; }

    /// <summary>The user's Purpose, if any.</summary>
    public string? Purpose { get; set; }

    /// <summary>The stored answers, off-path ones null.</summary>
    public PriorityWalkAnswersDto Answers { get; set; } = new();

    /// <summary>The leaf's dollar figures, recomputed from the stored answers.</summary>
    public PriorityLeafFiguresDto Figures { get; set; } = new();

    /// <summary>The "Your path" sentences, in path order, ending with the suggested priority.</summary>
    public List<string> PathSentences { get; set; } = [];

    /// <summary>Mermaid text of the whole tree with the path highlighted (light palette).</summary>
    public string WholeTreeDiagram { get; set; } = string.Empty;

    /// <summary>Mermaid text of the path nodes only, in one column, highlighted (light palette).</summary>
    public string PathViewDiagram { get; set; } = string.Empty;

    /// <summary>The tax year of the IRS limits used, when the path reached the limit question; otherwise null.</summary>
    public int? LimitsTaxYear { get; set; }

    /// <summary>The fixed rates behind the figures.</summary>
    public PriorityAssumptionsDto Assumptions { get; set; } = new();

    /// <summary>The estimate disclaimer shown under every result.</summary>
    public string Disclaimer { get; set; } = string.Empty;

    /// <summary>The Purpose Note saved onto the Walk, or null when none has been written yet.</summary>
    public string? PurposeNote { get; set; }
}

/// <summary>
/// The dollar figures for a leaf. Exactly the sections the leaf shows are set: <see cref="Debt"/> for L1,
/// <see cref="EmergencyFund"/> for L2, <see cref="EmployerMatch"/> for L3, <see cref="Scenarios"/> (with
/// <see cref="HorizonYears"/> and <see cref="TotalContributed"/>) for L4 to L7, and <see cref="RetirementSplit"/> for L6.
/// </summary>
public class PriorityLeafFiguresDto
{
    public string LeafId { get; set; } = string.Empty;
    public PriorityDebtFiguresDto? Debt { get; set; }
    public PriorityEmergencyFundFiguresDto? EmergencyFund { get; set; }
    public PriorityEmployerMatchFiguresDto? EmployerMatch { get; set; }
    public PriorityRetirementSplitDto? RetirementSplit { get; set; }

    /// <summary>The balance at the horizon under each scenario, lowest rate first.</summary>
    public List<PriorityScenarioBalanceDto> Scenarios { get; set; } = [];

    public int? HorizonYears { get; set; }
    public decimal? TotalContributed { get; set; }
}

/// <summary>
/// L1: paying the whole monthly amount toward the debt. <see cref="Months"/>, <see cref="TotalInterest"/> and
/// <see cref="FinalPayment"/> are set only when <see cref="Outcome"/> is <see cref="PriorityDebtOutcome.PaidOff"/>;
/// otherwise the page explains the outcome instead of showing figures.
/// </summary>
public class PriorityDebtFiguresDto
{
    public decimal Balance { get; set; }
    public decimal AprPercent { get; set; }

    /// <summary>The assumed investment return the APR is compared against.</summary>
    public decimal AssumedReturnPercent { get; set; }

    public PriorityDebtOutcome Outcome { get; set; }

    /// <summary>The interest the debt accrues in its first month.</summary>
    public decimal FirstMonthInterest { get; set; }

    /// <summary>
    /// The most months a payoff is simulated for; <see cref="PriorityDebtOutcome.LongerThanMaximum"/> means it takes
    /// longer than this.
    /// </summary>
    public int MaximumMonths { get; set; }

    /// <summary><see cref="MaximumMonths"/> in whole years, for the page's sentence about a payoff that takes longer.</summary>
    public int MaximumYears { get; set; }

    public int? Months { get; set; }
    public decimal? TotalInterest { get; set; }

    /// <summary>The last payment, capped at what was owed.</summary>
    public decimal? FinalPayment { get; set; }
}

/// <summary>L2: closing the gap to a 3-month emergency fund, with no growth.</summary>
public class PriorityEmergencyFundFiguresDto
{
    public decimal TargetMonths { get; set; }
    public decimal MonthsCovered { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal Gap { get; set; }
    public int MonthsToClose { get; set; }
}

/// <summary>L3: "up to" this much employer match a year; the employer's cap may be lower.</summary>
public class PriorityEmployerMatchFiguresDto
{
    public decimal MatchPercent { get; set; }
    public decimal YearlyMatch { get; set; }
    public decimal AssumedReturnPercent { get; set; }
}

/// <summary>L6: the yearly amount fills the IRA limit, then the 401(k) limit, then a taxable account.</summary>
public class PriorityRetirementSplitDto
{
    /// <summary>The tax year of the IRS limits used (the latest year when the current one has no row yet).</summary>
    public int TaxYear { get; set; }

    public RetirementAgeBand AgeBand { get; set; }
    public decimal YearlyAmount { get; set; }
    public decimal IraLimit { get; set; }
    public decimal EmployeeDeferralLimit { get; set; }
    public decimal ToIra { get; set; }
    public decimal ToEmployeePlan { get; set; }
    public decimal ToTaxable { get; set; }

    /// <summary>The Roth IRA phase-out ranges of that tax year, for the footnote (the page does not ask income).</summary>
    public decimal RothPhaseOutSingleStart { get; set; }

    public decimal RothPhaseOutSingleEnd { get; set; }
    public decimal RothPhaseOutJointStart { get; set; }
    public decimal RothPhaseOutJointEnd { get; set; }
}

/// <summary>The balance at the horizon under one scenario.</summary>
public class PriorityScenarioBalanceDto
{
    public PriorityGrowthScenario Scenario { get; set; }
    public decimal RatePercent { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>The fixed, stated assumptions behind every Money Priorities figure. Rates are yearly percentages.</summary>
public class PriorityAssumptionsDto
{
    public decimal BaseReturnPercent { get; set; }
    public decimal ConservativeReturnPercent { get; set; }
    public decimal AggressiveReturnPercent { get; set; }
    public decimal CashYieldPercent { get; set; }
}

/// <summary>One row of Earlier results.</summary>
public class PriorityWalkSummaryDto
{
    public int WalkId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal MonthlyAmount { get; set; }
    public string LeafId { get; set; } = string.Empty;
    public string LeafLabel { get; set; } = string.Empty;
}

/// <summary>The Priority Tree before a submit: the whole tree, nothing highlighted.</summary>
public class PriorityTreeDiagramDto
{
    public int TreeVersion { get; set; }

    /// <summary>Mermaid text of the whole tree, unhighlighted (light palette).</summary>
    public string WholeTreeDiagram { get; set; } = string.Empty;

    /// <summary>The thresholds the tree decides on, so the page shows the follow-up questions they call for.</summary>
    public PriorityTreeThresholdsDto Thresholds { get; set; } = new();
}

/// <summary>
/// The Priority Tree's thresholds, for showing and hiding the page's follow-up fields only: the ApiService alone
/// decides the path.
/// </summary>
public class PriorityTreeThresholdsDto
{
    /// <summary>A debt APR at or above this percentage is high-interest (asks for the balance).</summary>
    public decimal HighInterestAprPercent { get; set; }

    /// <summary>An emergency fund under this many months of expenses is short (asks for the monthly expenses).</summary>
    public decimal EmergencyFundTargetMonths { get; set; }

    /// <summary>A horizon over this many years is long (asks for the age range and the contribution limit).</summary>
    public int LongHorizonYears { get; set; }
}

/// <summary>
/// A rejected submit (400): every message in order, and the same messages keyed by the request property they belong
/// to (e.g. <c>MonthlyAmount</c>), so the page can show each one under its field.
/// </summary>
public class MoneyPrioritiesValidationErrorsDto
{
    public List<string> Errors { get; set; } = [];
    public Dictionary<string, List<string>> FieldErrors { get; set; } = [];
}

/// <summary>
/// A Priority Walk's Purpose Note, or why it has none (<see cref="State"/>). The note is plain text, shown as text and
/// never as HTML, with <see cref="Disclaimer"/> under it.
/// </summary>
public class PriorityWalkPurposeNoteDto
{
    public int WalkId { get; set; }
    public PurposeNoteState State { get; set; }

    /// <summary>The note; set only when <see cref="State"/> is <see cref="PurposeNoteState.Written"/>.</summary>
    public string? Note { get; set; }

    /// <summary>What to show instead of a note (the invitation, "not available right now", the quota message, the refusal).</summary>
    public string? Message { get; set; }

    /// <summary>The AI disclaimer shown under the note.</summary>
    public string Disclaimer { get; set; } = string.Empty;
}
