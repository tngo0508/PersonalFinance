using System.Globalization;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// Formats US$ amounts on the Research page compactly: <c>$3.02T</c>, <c>$412.5B</c>, <c>$18.0M</c>.
/// </summary>
public static class PublicFloatDisplay
{
    /// <summary>The amount in US$ with a T, B or M suffix (or whole dollars below a million).</summary>
    public static string Format(decimal amount)
    {
        CultureInfo us = CultureInfo.GetCultureInfo("en-US");
        decimal absolute = Math.Abs(amount);
        return absolute switch
        {
            >= 1_000_000_000_000m => (amount / 1_000_000_000_000m).ToString("$#,0.00", us) + "T",
            >= 1_000_000_000m => (amount / 1_000_000_000m).ToString("$#,0.0", us) + "B",
            >= 1_000_000m => (amount / 1_000_000m).ToString("$#,0.0", us) + "M",
            _ => amount.ToString("$#,0", us),
        };
    }
}
