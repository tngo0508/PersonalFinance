using PersonalFinance.Data.Entities;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// The ten Budget Planner categories in Budget-page order, with the Allocation Amount each one reads from a saved
/// <see cref="UserBudgetAllocation"/>. Readers that list categories (Dashboard, AI Assistant) use this list.
/// </summary>
public static class BudgetPlannerCategories
{
    /// <summary>Every category with its Allocation Amount, in Budget-page order (zero amounts included).</summary>
    public static IReadOnlyList<(string Name, decimal Amount)> AllocationAmounts(UserBudgetAllocation budget)
    {
        return
        [
            ("Housing", budget.HousingAmount),
            ("Food", budget.FoodAmount),
            ("Transportation", budget.TransportationAmount),
            ("Utilities", budget.UtilitiesAmount),
            ("Insurance", budget.InsuranceAmount),
            ("Healthcare", budget.HealthcareAmount),
            ("Savings", budget.SavingsAmount),
            ("Debt Repayment", budget.DebtRepaymentAmount),
            ("Entertainment", budget.EntertainmentAmount),
            ("Miscellaneous", budget.MiscellaneousAmount)
        ];
    }
}
