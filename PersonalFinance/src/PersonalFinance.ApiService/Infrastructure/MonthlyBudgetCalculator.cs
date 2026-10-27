using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Infrastructure;

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
/// It works on amounts already in the Reporting Currency (ADR 0016): <see cref="InReportingCurrency"/> and
/// <see cref="TransactionSums"/> bring the stored lines and transactions there first.
/// </summary>
public static class MonthlyBudgetCalculator
{
    /// <summary>
    /// A copy of <paramref name="line"/> with its planned amount and override in the Reporting Currency, at the rate of
    /// the first day of its month. The stored line is left as it is; the sheet shows its original beside the figure.
    /// </summary>
    public static MonthlyBudgetLine InReportingCurrency(MonthlyBudgetLine line, ReportingConversion conversion,
        DateOnly monthStart)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(conversion);
        return new MonthlyBudgetLine
        {
            Id = line.Id,
            CategoryId = line.CategoryId,
            CurrencyCode = conversion.ReportingCurrency,
            PlannedAmount = conversion.ConvertOrOriginal(line.PlannedAmount, line.CurrencyCode, monthStart),
            ActualOverride = line.ActualOverride is { } overrideAmount
                ? conversion.ConvertOrOriginal(overrideAmount, line.CurrencyCode, monthStart)
                : null
        };
    }

    /// <summary>Each category's transactions summed in the Reporting Currency, each at its own day's rate.</summary>
    public static Dictionary<int, decimal> TransactionSums(IEnumerable<BudgetTransaction> transactions,
        ReportingConversion conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        return transactions
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(t => conversion.ConvertOrOriginal(t.Amount, t.CurrencyCode, t.Date)));
    }

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

        IOrderedEnumerable<BudgetCategory> visibleCategories = allCategories
            .Where(c => !c.IsDeleted ||
                        (lineMap.TryGetValue(c.Id, out MonthlyBudgetLine? l) && (l.PlannedAmount > 0 || l.ActualOverride.HasValue)) ||
                        txSumByCategory.ContainsKey(c.Id))
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id);

        foreach (BudgetCategory? cat in visibleCategories)
        {
            lineMap.TryGetValue(cat.Id, out MonthlyBudgetLine? line);
            decimal planned = line?.PlannedAmount ?? 0m;
            bool hasOverride = line?.ActualOverride.HasValue == true;
            decimal actual = hasOverride
                ? line!.ActualOverride!.Value
                : (txSumByCategory.TryGetValue(cat.Id, out decimal txSum) ? txSum : 0m);

            decimal diff = cat.Kind == BudgetCategoryKind.Expense
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
