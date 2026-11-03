using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.FinancialUniverse;

/// <summary>
/// Where each <see cref="FinancialUniverseLinkTarget"/> goes in the Web app. The ApiService names the feature; only the
/// Web layer knows its route, so a URL never crosses the API.
/// </summary>
public static class FinancialUniverseLinks
{
    /// <summary>The MVC controller and action of <paramref name="target"/>.</summary>
    public static (string Controller, string Action) Route(FinancialUniverseLinkTarget target) => target switch
    {
        FinancialUniverseLinkTarget.BudgetTracker => ("BudgetTracker", "Index"),
        FinancialUniverseLinkTarget.Analysis => ("Analysis", "Index"),
        FinancialUniverseLinkTarget.SavingsGoals => ("SavingsGoals", "Index"),
        FinancialUniverseLinkTarget.NetWorth => ("Networth", "Index"),
        FinancialUniverseLinkTarget.InvestmentCalculator => ("InvestmentCalculator", "Index"),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown Financial Universe link target.")
    };
}
