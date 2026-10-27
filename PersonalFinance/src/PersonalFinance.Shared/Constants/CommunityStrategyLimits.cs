namespace PersonalFinance.Shared.Constants;

/// <summary>
/// What a Strategy (GLOSSARY.md) accepts, shared by the database schema, the ApiService validator and the Web form:
/// the field sizes, and the categories the board filters by. A category is stored in English; the Web translates its
/// label.
/// </summary>
public static class CommunityStrategyLimits
{
    /// <summary>Maximum length of a Strategy's title.</summary>
    public const int TitleMaxLength = 120;

    /// <summary>Maximum length of each of the Tried, Happened and Keeping texts.</summary>
    public const int TextMaxLength = 1000;

    /// <summary>Maximum length of a stored category.</summary>
    public const int CategoryMaxLength = 50;

    /// <summary>The most Strategies the board shows at once, the viewer's own included.</summary>
    public const int BoardSize = 60;

    /// <summary>The categories a Strategy can have, in the order the board's chips show them.</summary>
    public static IReadOnlyList<string> Categories { get; } =
        ["Spending", "Groceries", "Saving", "Debt", "Investing", "Income", "Habits", "Other"];
}
