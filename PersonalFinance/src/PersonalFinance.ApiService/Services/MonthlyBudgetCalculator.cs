using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Calculated category row resulting from monthly budget rules.
/// </summary>
public sealed class CalculatedBudgetCategoryRow
{
    public int CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public BudgetCategoryKind Kind { get; init; }
    public int SortOrder { get; init; }
    public decimal Planned { get; init; }
    public decimal Actual { get; init; }
    public decimal Diff { get; init; }
    public bool ActualIsOverride { get; init; }
    public bool IsArchived { get; init; }
}

/// <summary>
/// Server-side calculator encapsulating monthly budget row visibility, effective Actual calculation, and Diff formulas.
/// </summary>
public static class MonthlyBudgetCalculator
{
    /// <summary>
    /// Builds calculated rows for categories in a monthly plan.
    /// Active categories are always included. Archived (soft-deleted) categories are included only if they
    /// have a planned amount, an actual override, or transactions in the month.
    /// Effective Actual is the manual override when present, otherwise the sum of transactions for that category.
    /// Diff is Planned - Actual for Expenses, and Actual - Planned for Income.
    /// </summary>
    public static List<CalculatedBudgetCategoryRow> BuildRows(
        IEnumerable<BudgetCategory> allCategories,
        IReadOnlyDictionary<int, MonthlyBudgetLine> lineMap,
        IReadOnlyDictionary<int, decimal> txSumByCategory)
    {
        var result = new List<CalculatedBudgetCategoryRow>();

        var visibleCategories = allCategories
            .Where(c => !c.IsDeleted ||
                        (lineMap.TryGetValue(c.Id, out var l) && (l.PlannedAmount > 0 || l.ActualOverride.HasValue)) ||
                        txSumByCategory.ContainsKey(c.Id))
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id);

        foreach (var cat in visibleCategories)
        {
            lineMap.TryGetValue(cat.Id, out var line);
            var planned = line?.PlannedAmount ?? 0m;
            var hasOverride = line?.ActualOverride.HasValue == true;
            var actual = hasOverride
                ? line!.ActualOverride!.Value
                : (txSumByCategory.TryGetValue(cat.Id, out var txSum) ? txSum : 0m);

            var diff = cat.Kind == BudgetCategoryKind.Expense
                ? planned - actual
                : actual - planned;

            result.Add(new CalculatedBudgetCategoryRow
            {
                CategoryId = cat.Id,
                Name = cat.Name,
                Kind = cat.Kind,
                SortOrder = cat.SortOrder,
                Planned = planned,
                Actual = actual,
                Diff = diff,
                ActualIsOverride = hasOverride,
                IsArchived = cat.IsDeleted
            });
        }

        return result;
    }
}