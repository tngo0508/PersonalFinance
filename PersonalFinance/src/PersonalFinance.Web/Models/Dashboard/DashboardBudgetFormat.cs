using System.Globalization;

namespace PersonalFinance.Web.Models.Dashboard;

/// <summary>
/// Presentation helpers for the Dashboard budget breakdown (card and key-figure tile).
/// Values arrive unrounded from the Dashboard service; rounding happens only here.
/// </summary>
public static class DashboardBudgetFormat
{
    /// <summary>
    /// Formats a stored budget period ("yyyy-MM") as a month label in the request culture, such as "October 2026"
    /// (en-US) or "Tháng 10 năm 2026" (vi-VN). Unrecognised values are returned unchanged so the card never hides
    /// what was stored.
    /// </summary>
    public static string PeriodLabel(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return string.Empty;
        }

        return DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out DateTime month)
            ? MonthLabel(month.Year, month.Month)
            : period;
    }

    /// <summary>A calendar month as a label in the request culture, such as "October 2026".</summary>
    public static string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1).ToString("Y", CultureInfo.CurrentCulture);

    /// <summary>
    /// Formats a percentage with at most one decimal place and the request culture's decimal separator, e.g. "12.5%"
    /// (en-US) or "12,5%" (vi-VN).
    /// </summary>
    public static string Percent(decimal percent) =>
        $"{Math.Round(percent, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.CurrentCulture)}%";
}
