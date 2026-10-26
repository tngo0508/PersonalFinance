using System.Globalization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Formats a typed argument of an API error message (ADR 0017) for the reader's culture. The ApiService writes values
/// culture-invariant; a value that does not parse is shown as given rather than dropped.
/// </summary>
internal static class ApiErrorArgFormat
{
    private const string GroupedNumber = "#,0.############";

    /// <summary>
    /// <see cref="ApiErrorArgKind.Text"/> as is; <see cref="ApiErrorArgKind.Number"/> grouped (<c>2,000</c> in en-US,
    /// <c>2.000</c> in vi-VN); <see cref="ApiErrorArgKind.Money"/> through <see cref="MoneyFormatter"/>, in whole units
    /// when the amount has no fraction.
    /// </summary>
    public static string Format(ApiErrorArgDto arg, CultureInfo culture)
    {
        if (arg.Kind == ApiErrorArgKind.Text ||
            !decimal.TryParse(arg.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            return arg.Value;
        }

        return arg.Kind == ApiErrorArgKind.Money
            ? FormatMoney(arg, value, culture)
            : value.ToString(GroupedNumber, culture);
    }

    private static string FormatMoney(ApiErrorArgDto arg, decimal value, CultureInfo culture)
    {
        if (arg.Currency is not { } currency || !SupportedCurrencies.IsSupported(currency))
        {
            return arg.Value;
        }

        return value == decimal.Truncate(value)
            ? MoneyFormatter.FormatWhole(value, currency, culture)
            : MoneyFormatter.Format(value, currency, culture);
    }
}
