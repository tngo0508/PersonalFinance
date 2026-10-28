using System.Globalization;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// The upper bounds the money inputs are checked against, in the currency the amount is in (#444). Each bound is written
/// once, in whole US dollars (<see cref="BudgetPlannerLimits"/>, <see cref="NetWorthLimits"/>,
/// <see cref="MoneyPrioritiesLimits"/>), and multiplied by the currency's <see cref="SupportedCurrency.BoundScale"/>:
/// a 10,000,000 dollar bound is 250,000,000,000 ₫. The Web attributes, the ApiService validators and the page scripts
/// (through the <c>data-max-usd</c> and <c>data-bound-scale</c> attributes the views write) all read this one table,
/// so the three layers agree. The bounds guard the inputs only; a <c>decimal(18,2)</c> column holds any of them. Every
/// bound starts at 0.
/// </summary>
public static class MoneyBounds
{
    /// <summary>
    /// <paramref name="dollarBound"/> in <paramref name="currencyCode"/>. A missing or unsupported code reads as
    /// <see cref="SupportedCurrencies.Default"/>; a validator rejects an unsupported code on its own.
    /// </summary>
    public static decimal Max(long dollarBound, string? currencyCode) => dollarBound * (decimal)Currency(currencyCode).BoundScale;

    /// <summary><see cref="Max"/> written for an HTML attribute (<c>250000000000</c>), whatever the request culture.</summary>
    public static string MaxInvariant(long dollarBound, string? currencyCode) =>
        Max(dollarBound, currencyCode).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// <paramref name="template"/> with its <c>{min}</c> and <c>{max}</c> filled as whole amounts in
    /// <paramref name="currencyCode"/> (<c>$0</c> and <c>$1,000,000</c>; <c>₫0</c> and <c>₫25,000,000,000</c>), in the
    /// invariant culture: the English fallback text of a keyed rejection (ADR 0017).
    /// </summary>
    public static string Describe(string template, long dollarBound, string? currencyCode)
    {
        string code = Currency(currencyCode).Code;
        return template
            .Replace("{min}", MoneyFormatter.FormatWhole(0m, code, CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{max}", MoneyFormatter.FormatWhole(Max(dollarBound, code), code, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static SupportedCurrency Currency(string? currencyCode) =>
        SupportedCurrencies.Find(currencyCode) ?? SupportedCurrencies.Default;
}
