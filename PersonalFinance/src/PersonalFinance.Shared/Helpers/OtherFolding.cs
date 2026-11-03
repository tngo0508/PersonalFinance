namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// The rule that folds small categories into one "Other" (Money Flow on Analysis, Financial Universe): a category
/// below <see cref="Threshold"/> of the month's total flow, or past the <see cref="MaxColoredCategories"/> that get a
/// colour of their own, joins Other. Categories are offered in display order (largest first), and each kept one takes
/// the next colour slot; colours are never cycled, so a sixth category always folds.
/// </summary>
public static class OtherFolding
{
    /// <summary>Categories below this share of the month's total flow are grouped into Other.</summary>
    public const decimal Threshold = 0.03m;

    /// <summary>
    /// How many categories get their own colour. The palette is validated for neighbouring bands (money-flow.js), so
    /// further categories join Other instead of reusing a colour.
    /// </summary>
    public const int MaxColoredCategories = 5;

    /// <summary>
    /// Whether a category of <paramref name="amount"/> folds into Other when <paramref name="keptSoFar"/> categories
    /// already have a colour and the month's total flow is <paramref name="totalFlow"/>.
    /// </summary>
    public static bool IsOther(decimal amount, decimal totalFlow, int keptSoFar) =>
        amount < totalFlow * Threshold || keptSoFar >= MaxColoredCategories;

    /// <summary>
    /// Splits <paramref name="categoriesInDisplayOrder"/> into the ones kept, each with its colour slot in order, and
    /// the ones folded into Other, with their total.
    /// </summary>
    public static OtherFoldingResult<T> Fold<T>(IEnumerable<T> categoriesInDisplayOrder, Func<T, decimal> amount,
        decimal totalFlow)
    {
        ArgumentNullException.ThrowIfNull(categoriesInDisplayOrder);
        ArgumentNullException.ThrowIfNull(amount);

        var kept = new List<ColoredCategory<T>>();
        var folded = new List<T>();
        decimal otherTotal = 0m;
        foreach (T category in categoriesInDisplayOrder)
        {
            decimal value = amount(category);
            if (IsOther(value, totalFlow, kept.Count))
            {
                folded.Add(category);
                otherTotal += value;
                continue;
            }

            kept.Add(new ColoredCategory<T>(category, kept.Count));
        }

        return new OtherFoldingResult<T>(kept, folded, otherTotal);
    }
}

/// <summary>A category that keeps its own node, and the colour slot it took (0 first).</summary>
public sealed record ColoredCategory<T>(T Category, int ColorSlot);

/// <summary>The outcome of <see cref="OtherFolding.Fold{T}"/>.</summary>
public sealed record OtherFoldingResult<T>(
    IReadOnlyList<ColoredCategory<T>> Kept,
    IReadOnlyList<T> Folded,
    decimal OtherTotal);
