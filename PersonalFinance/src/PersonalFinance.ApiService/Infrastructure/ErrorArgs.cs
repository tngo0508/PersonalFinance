using System.Globalization;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     Builds the typed, culture-invariant arguments of an API error message (ADR 0017). Numbers are written with the
///     invariant culture and without trailing zeros (<c>10000000</c>, not <c>10,000,000.00</c>); the Web app formats
///     them for the reader.
/// </summary>
public static class ErrorArgs
{
    private const string InvariantDecimal = "0.############################";

    /// <summary>One argument shown as is.</summary>
    public static IReadOnlyDictionary<string, ApiErrorArgDto> Text(string name, string value) =>
        new Dictionary<string, ApiErrorArgDto> { [name] = new() { Kind = ApiErrorArgKind.Text, Value = value } };

    /// <summary>One number argument.</summary>
    public static IReadOnlyDictionary<string, ApiErrorArgDto> Number(string name, decimal value) =>
        new Dictionary<string, ApiErrorArgDto>
            { [name] = new() { Kind = ApiErrorArgKind.Number, Value = Format(value) } };

    /// <summary>One amount argument in <paramref name="currencyCode"/>.</summary>
    public static IReadOnlyDictionary<string, ApiErrorArgDto> Money(string name, decimal value, string currencyCode) =>
        new Dictionary<string, ApiErrorArgDto> { [name] = MoneyArg(value, currencyCode) };

    /// <summary>The <c>min</c> and <c>max</c> amount arguments of a range message.</summary>
    public static IReadOnlyDictionary<string, ApiErrorArgDto>
        MoneyRange(decimal min, decimal max, string currencyCode) =>
        new Dictionary<string, ApiErrorArgDto>
        {
            ["min"] = MoneyArg(min, currencyCode),
            ["max"] = MoneyArg(max, currencyCode)
        };

    private static ApiErrorArgDto MoneyArg(decimal value, string currencyCode) =>
        new() { Kind = ApiErrorArgKind.Money, Value = Format(value), Currency = currencyCode };

    private static string Format(decimal value) => value.ToString(InvariantDecimal, CultureInfo.InvariantCulture);
}
