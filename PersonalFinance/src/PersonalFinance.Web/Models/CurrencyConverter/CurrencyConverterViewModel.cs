using System.Globalization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.CurrencyConverter;

namespace PersonalFinance.Web.Models.CurrencyConverter;

/// <summary>
/// The Currency Converter page: the form's values (from the query string, or the defaults from the Account's
/// Reporting Currency), the currencies it offers, and the result of the last submit (<c>null</c> before the first).
/// </summary>
public sealed class CurrencyConverterViewModel
{
    /// <summary>How many significant digits the rate is shown with.</summary>
    public const int RateSignificantDigits = 6;

    /// <summary>The earliest day the date field accepts.</summary>
    public static DateOnly MinDate => ConvertibleCurrencies.FirstRateDate;

    /// <summary>The amount to convert; empty before the first submit.</summary>
    public decimal? Amount { get; set; }

    /// <summary>The ISO 4217 code converted from.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>The ISO 4217 code converted into.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>The day whose rate to use; empty for the latest.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>The latest day the date field accepts: today (UTC).</summary>
    public DateOnly MaxDate { get; set; }

    /// <summary>Every Convertible Currency, in the order offered.</summary>
    public IReadOnlyList<ConvertibleCurrency> Currencies { get; set; } = ConvertibleCurrencies.All;

    /// <summary>The conversion of the last submit; <c>null</c> before the first or when the form was rejected.</summary>
    public CurrencyConversionDto? Result { get; set; }

    /// <summary>
    /// <paramref name="rate"/> to <see cref="RateSignificantDigits"/> significant digits, halves away from zero, without
    /// trailing zeros, in <paramref name="culture"/>: <c>164.213</c>, <c>25,500</c>, <c>0.00609756</c>.
    /// </summary>
    public static string FormatRate(decimal rate, CultureInfo culture)
    {
        if (rate <= 0m)
        {
            return 0m.ToString(culture);
        }

        int exponent = 0;
        for (decimal scaled = rate; scaled >= 10m; scaled /= 10m)
        {
            exponent++;
        }

        for (decimal scaled = rate; scaled < 1m; scaled *= 10m)
        {
            exponent--;
        }

        int decimals = RateSignificantDigits - 1 - exponent;
        if (decimals < 0)
        {
            // A rate of a million or more: round its whole units to the significant digits too.
            decimal unit = 1m;
            for (int i = 0; i < -decimals; i++)
            {
                unit *= 10m;
            }

            decimal whole = decimal.Round(rate / unit, 0, MidpointRounding.AwayFromZero) * unit;
            return whole.ToString("#,##0", culture);
        }

        decimals = Math.Min(decimals, 20);
        decimal rounded = decimal.Round(rate, decimals, MidpointRounding.AwayFromZero);
        return rounded.ToString("#,##0." + new string('#', decimals), culture);
    }
}
