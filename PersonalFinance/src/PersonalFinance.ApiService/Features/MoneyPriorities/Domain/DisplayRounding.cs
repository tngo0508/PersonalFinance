namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The one rounding a Money Priorities figure gets, at display (spec #315, "Display"). The calculators never call it:
/// they return full precision. Use it only where C# itself shows a computed figure (for example text sent to the AI
/// Purpose Note); the page formats the API's full-precision values itself.
/// </summary>
internal static class DisplayRounding
{
    /// <summary>A computed figure in whole US dollars, midpoint away from zero: 86,542.40 is 86,542 and 0.50 is 1.</summary>
    public static decimal WholeDollars(decimal amount) => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}
