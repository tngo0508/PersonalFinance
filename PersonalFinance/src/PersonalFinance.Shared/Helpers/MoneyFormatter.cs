using System.Globalization;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// Formats an amount of money for display (localization spec §2.2, ADR 0016). The symbol and the minor units come from
/// the currency, the separators and the symbol's position from the culture: <c>$1,234.56</c> in en-US,
/// <c>1.234.567 ₫</c> in vi-VN. The culture's own currency symbol is never used, so a <c>"C"</c> format is banned
/// everywhere else. The same rules live in <c>wwwroot/js/core/i18n.js</c> (<c>PFI18n.formatMoney</c>).
/// </summary>
public static class MoneyFormatter
{
    /// <summary>
    /// <paramref name="amount"/> in <paramref name="currencyCode"/> with that currency's minor units, rounded half away
    /// from zero, in <paramref name="culture"/> (the current culture when omitted).
    /// </summary>
    /// <exception cref="ArgumentException">The currency is not in <see cref="SupportedCurrencies"/>.</exception>
    public static string Format(decimal amount, string currencyCode, CultureInfo? culture = null) =>
        Format(amount, Currency(currencyCode), wholeUnits: false, culture ?? CultureInfo.CurrentCulture);

    /// <summary>
    /// <paramref name="amount"/> rounded half away from zero to whole units (<c>$1,235</c>), for summaries and charts
    /// that drop the cents.
    /// </summary>
    /// <exception cref="ArgumentException">The currency is not in <see cref="SupportedCurrencies"/>.</exception>
    public static string FormatWhole(decimal amount, string currencyCode, CultureInfo? culture = null) =>
        Format(amount, Currency(currencyCode), wholeUnits: true, culture ?? CultureInfo.CurrentCulture);

    private static SupportedCurrency Currency(string currencyCode) =>
        SupportedCurrencies.Find(currencyCode)
        ?? throw new ArgumentException($"'{currencyCode}' is not a supported currency.", nameof(currencyCode));

    private static string Format(decimal amount, SupportedCurrency currency, bool wholeUnits, CultureInfo culture)
    {
        int decimals = wholeUnits ? 0 : currency.MinorUnits;
        var numberFormat = (NumberFormatInfo)culture.NumberFormat.Clone();
        numberFormat.CurrencySymbol = currency.Symbol;
        numberFormat.CurrencyDecimalDigits = decimals;

        decimal rounded = Math.Round(amount, decimals, MidpointRounding.AwayFromZero);
        // -0.004 rounds to 0: show "$0.00", not "-$0.00". A space between number and symbol ("1.234 ₫") is made
        // non-breaking, as the browser's Intl.NumberFormat writes it, so server and client output match and never wrap.
        return (rounded == 0m ? 0m : rounded).ToString("C", numberFormat).Replace(' ', ' ');
    }
}
