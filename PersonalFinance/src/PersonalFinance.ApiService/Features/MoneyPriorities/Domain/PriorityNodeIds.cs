namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>The node ids of the Priority Tree. They are stored on every Priority Walk, so never renumber them.</summary>
internal static class PriorityNodeIds
{
    /// <summary>Start: extra money each month.</summary>
    public const string Start = "S";

    /// <summary>Q1: debt with an APR of 7% or more?</summary>
    public const string HighInterestDebt = "Q1";

    /// <summary>Q2: emergency fund covers 3 months of expenses?</summary>
    public const string EmergencyFund = "Q2";

    /// <summary>Q3: getting the full employer match?</summary>
    public const string EmployerMatch = "Q3";

    /// <summary>Q4: when will you need this money?</summary>
    public const string Horizon = "Q4";

    /// <summary>Q5: already at the IRS limit for your age?</summary>
    public const string ContributionLimit = "Q5";

    /// <summary>L1: pay down the high-interest debt first.</summary>
    public const string PayDebt = "L1";

    /// <summary>L2: build the emergency fund to 3 months.</summary>
    public const string BuildEmergencyFund = "L2";

    /// <summary>L3: raise 401(k) contributions to capture the full match.</summary>
    public const string CaptureMatch = "L3";

    /// <summary>L4: high-yield savings or short-term Treasuries.</summary>
    public const string ShortTermSavings = "L4";

    /// <summary>L5: taxable brokerage, balanced index funds.</summary>
    public const string BalancedBrokerage = "L5";

    /// <summary>L6: max the IRA, then the 401(k).</summary>
    public const string IraThen401k = "L6";

    /// <summary>L7: taxable brokerage, stock index funds.</summary>
    public const string StockBrokerage = "L7";
}
