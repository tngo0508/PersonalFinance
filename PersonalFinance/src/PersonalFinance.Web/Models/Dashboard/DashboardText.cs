using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.Dashboard;

/// <summary>
/// The Dashboard's wording for values the Dashboard service sends in English or as raw fields: Budget Planner category
/// names, recent activity types and descriptions. User data (transaction categories, goal names) is never translated.
/// </summary>
public static class DashboardText
{
    private const string CategoryKeyPrefix = "Common.BudgetCategory.";

    /// <summary>
    /// A Budget Planner category's name in the request's Language, by its key ("debt-repayment" reads
    /// <c>Common.BudgetCategory.DebtRepayment</c>); the service's English name when the key is not one the app knows.
    /// </summary>
    public static string CategoryName(IStringLocalizer localizer, BudgetAllocationDto allocation)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(allocation);
        if (string.IsNullOrEmpty(allocation.Key))
        {
            return allocation.Name;
        }

        string pascal = string.Concat(allocation.Key.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        LocalizedString name = localizer[CategoryKeyPrefix + pascal];
        return name.ResourceNotFound ? allocation.Name : name.Value;
    }

    /// <summary>The screen-reader label for an activity's type: Income, Expense, Savings goal, or Activity.</summary>
    public static string ActivityType(IStringLocalizer localizer, string type)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return type switch
        {
            "Income" => localizer["Dashboard.Activity.Type.Income"],
            "Expense" => localizer["Dashboard.Activity.Type.Expense"],
            "SavingsGoal" => localizer["Dashboard.Activity.Type.SavingsGoal"],
            _ => localizer["Dashboard.Activity.Type.Other"]
        };
    }

    /// <summary>
    /// An activity's description in the request's Language and culture: "{category} - {amount}" for a transaction,
    /// "Target: {target} | Saved: {saved}" for a savings goal. Falls back to the service's English description when
    /// the item carries no amounts.
    /// </summary>
    public static string ActivityDescription(IStringLocalizer localizer, RecentActivityDto activity)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(activity);
        string currency = SupportedCurrencies.Default.Code;

        if (activity is { TargetAmount: { } target, SavedAmount: { } saved })
        {
            return localizer.Named("Dashboard.Activity.GoalDescription",
                ("target", MoneyFormatter.Format(target, currency)),
                ("saved", MoneyFormatter.Format(saved, currency)));
        }

        if (activity.Amount is { } amount)
        {
            return localizer.Named("Dashboard.Activity.TransactionDescription",
                ("category", activity.Category ?? string.Empty),
                ("amount", MoneyFormatter.Format(amount, currency)));
        }

        return activity.Description;
    }
}
