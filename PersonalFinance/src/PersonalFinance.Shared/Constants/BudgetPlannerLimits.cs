namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The range a Budget Planner amount must fall in, shared by the Web form, the ApiService validator and the page's
/// script. Written in whole US dollars; <see cref="Helpers.MoneyBounds"/> scales it to the budget's currency (#444).
/// </summary>
public static class BudgetPlannerLimits
{
    /// <summary>The largest monthly income or category allocation, in US dollars. Every amount is also at least 0.</summary>
    public const long MaxAmount = 10_000_000;
}
