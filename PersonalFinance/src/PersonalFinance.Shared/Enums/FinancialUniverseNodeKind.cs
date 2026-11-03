namespace PersonalFinance.Shared.Enums;

/// <summary>
/// What a node of the Financial Universe (GLOSSARY.md) stands for, in the graph's left-to-right rank order: income
/// sources pool into Income, which flows into the expense categories (and Other) and into Savings or a Shortfall;
/// Savings feeds the Savings Goals and relates to Net Worth, which relates to the Investment Scenarios.
/// </summary>
public enum FinancialUniverseNodeKind
{
    /// <summary>One income category of the budget source, with the month's actual amount.</summary>
    IncomeSource = 1,

    /// <summary>The month's pooled income.</summary>
    Income = 2,

    /// <summary>One expense category with the month's actual amount.</summary>
    ExpenseCategory = 3,

    /// <summary>The expense categories under the Other threshold, folded together.</summary>
    Other = 4,

    /// <summary>Income minus expenses, when positive.</summary>
    Savings = 5,

    /// <summary>Expenses minus income, when the month spent more than it earned.</summary>
    Shortfall = 6,

    /// <summary>One Savings Goal, with its saved and target amounts.</summary>
    SavingsGoal = 7,

    /// <summary>The Net Worth parameters: starting balance and annual savings.</summary>
    NetWorth = 8,

    /// <summary>One saved Investment Scenario.</summary>
    InvestmentScenario = 9,

    /// <summary>Stands in for a kind the Account has nothing of yet, linking to the feature that fills it.</summary>
    Placeholder = 10
}
