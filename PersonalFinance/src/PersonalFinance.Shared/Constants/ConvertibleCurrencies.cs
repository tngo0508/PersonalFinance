namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The currencies the currency converter can convert between (ADR 0016): the rate refresher stores a daily USD rate for
/// each. Wider than <see cref="SupportedCurrencies"/>, which every one of is here too: an amount still stores only a
/// supported currency. Adding one is an entry here and its name in <c>en.json</c>; the schema does not change.
/// </summary>
public static class ConvertibleCurrencies
{
    /// <summary>Every convertible currency, in the order the converter offers them.</summary>
    public static readonly IReadOnlyList<ConvertibleCurrency> All =
    [
        Create("USD", 2),
        Create("VND", 0),
        Create("JPY", 0),
        Create("EUR", 2),
        Create("GBP", 2),
        Create("CNY", 2),
        Create("KRW", 0),
        Create("THB", 2),
        Create("SGD", 2),
        Create("AUD", 2),
        Create("CAD", 2)
    ];

    /// <summary>The currency with ISO 4217 <paramref name="code"/> (exact, upper case); <c>null</c> when it is not convertible.</summary>
    public static ConvertibleCurrency? Find(string? code) =>
        All.FirstOrDefault(currency => string.Equals(currency.Code, code, StringComparison.Ordinal));

    /// <summary>Whether <paramref name="code"/> is a convertible ISO 4217 code (exact, upper case).</summary>
    public static bool IsConvertible(string? code) => Find(code) is not null;

    /// <summary>
    /// <paramref name="amount"/> rounded to the minor units of the convertible currency <paramref name="code"/>; throws
    /// for a code that is not convertible.
    /// </summary>
    public static decimal Round(string code, decimal amount) =>
        (Find(code) ?? throw new ArgumentException($"'{code}' is not a convertible currency.", nameof(code))).Round(amount);

    private static ConvertibleCurrency Create(string code, int minorUnits) =>
        new() { Code = code, MinorUnits = minorUnits, NameKey = $"Common.Currency.Name.{code}" };
}
