namespace PersonalFinance.Web.Models.Research;

/// <summary>The date formats every Research page uses.</summary>
public static class ResearchFormats
{
    /// <summary>
    /// The pages' date format, "31 December 2025" in English: day, month name and year in this order; only the month
    /// name comes from the request culture.
    /// </summary>
    public const string LongDate = "d MMMM yyyy";

    /// <summary>The pages' month format, the culture's year-month pattern ("December 2025").</summary>
    public const string MonthYear = "Y";
}
