namespace PersonalFinance.Shared.Enums;

/// <summary>
/// The feature page a Financial Universe node opens. The ApiService names the page; the Web layer turns it into a URL,
/// so no URL ever crosses the API.
/// </summary>
public enum FinancialUniverseLinkTarget
{
    /// <summary>The Budget Tracker: income and expense categories kept in the app.</summary>
    BudgetTracker = 1,

    /// <summary>Analysis: the synced Google Sheet's months, and Money Flow's Savings and Shortfall.</summary>
    Analysis = 2,

    /// <summary>The Savings Goals page.</summary>
    SavingsGoals = 3,

    /// <summary>The Net Worth page.</summary>
    NetWorth = 4,

    /// <summary>The Investment Calculator, where Investment Scenarios are saved.</summary>
    InvestmentCalculator = 5
}
