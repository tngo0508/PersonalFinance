using System.Globalization;

namespace PersonalFinance.Web.Models;

/// <summary>
/// Presentation helpers for the Dashboard budget breakdown (card and key-figure tile).
/// Values arrive unrounded from the Dashboard service; rounding happens only here.
/// </summary>
public static class DashboardBudgetFormat
{
    /// <summary>
    /// Formats a stored budget period ("yyyy-MM") as a month label such as "October 2026".
    /// Unrecognised values are returned unchanged so the card never hides what was stored.
    /// </summary>
    public static string PeriodLabel(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return string.Empty;
        }

        return DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var month)
            ? month.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : period;
    }

    /// <summary>Formats a percentage with at most one decimal place, e.g. "12.5%" or "30%".</summary>
    public static string Percent(decimal percent) =>
        $"{Math.Round(percent, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture)}%";
}
