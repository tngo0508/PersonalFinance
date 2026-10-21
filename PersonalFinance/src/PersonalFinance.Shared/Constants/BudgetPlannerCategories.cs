using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The ten Budget Planner categories in Budget-page order, with the Allocation Amount each one reads from
/// <see cref="BudgetAllocationsDto"/>. Web (chart data) and ApiService (Dashboard, AI Assistant) list categories with
/// this method; <c>wwwroot/js/budget/budget.js</c> keeps the same order for the form fields.
/// </summary>
public static class BudgetPlannerCategories
{
    /// <summary>Every category with its Allocation Amount, in Budget-page order (zero amounts included).</summary>
    public static IReadOnlyList<(string Name, decimal Amount)> AllocationAmounts(BudgetAllocationsDto allocations)
    {
        return
        [
            ("Housing", allocations.Housing),
            ("Food", allocations.Food),
            ("Transportation", allocations.Transportation),
            ("Utilities", allocations.Utilities),
            ("Insurance", allocations.Insurance),
            ("Healthcare", allocations.Healthcare),
            ("Savings", allocations.Savings),
            ("Debt Repayment", allocations.DebtRepayment),
            ("Entertainment", allocations.Entertainment),
            ("Miscellaneous", allocations.Miscellaneous)
        ];
    }
}
