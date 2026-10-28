namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The currencies an amount may be in (ADR 0016, localization spec §2.5). Adding one is an entry here (with its
/// <see cref="SupportedCurrency.BoundScale"/> for the money range bounds) plus, when it is not one already, an entry in
/// <see cref="ConvertibleCurrencies"/>, whose currencies the rate refresher quotes; the schema does not change. No enum and no lookup table. Every money-bearing row stores one of
/// these codes in its <c>CurrencyCode</c> column (slice C1).
/// </summary>
public static class SupportedCurrencies
{
    /// <summary>The length of every ISO 4217 code: a <c>CurrencyCode</c> column is fixed at it.</summary>
    public const int CodeLength = 3;

    /// <summary>
    /// The code of <see cref="Default"/>, as a constant for the schema: the default of every <c>CurrencyCode</c> column,
    /// so rows saved before slice C1 are US dollars.
    /// </summary>
    public const string DefaultCode = "USD";

    /// <summary>US dollar: two minor-unit decimals.</summary>
    public static readonly SupportedCurrency Usd = new()
        { Code = "USD", DisplayName = "US dollar", MinorUnits = 2, Symbol = "$", BoundScale = 1 };

    /// <summary>Vietnamese đồng: no minor units in use.</summary>
    public static readonly SupportedCurrency Vnd = new()
        { Code = "VND", DisplayName = "Vietnamese đồng", MinorUnits = 0, Symbol = "₫", BoundScale = 25_000 };

    /// <summary>
    /// The currency of a new amount whose request names none, and of every row saved before amounts carried their own.
    /// </summary>
    public static SupportedCurrency Default => Usd;

    /// <summary>Every supported currency.</summary>
    public static readonly IReadOnlyList<SupportedCurrency> All = [Usd, Vnd];

    /// <summary>The currency with ISO 4217 <paramref name="code"/> (exact, upper case); <c>null</c> when it is not supported.</summary>
    public static SupportedCurrency? Find(string? code) =>
        All.FirstOrDefault(currency => string.Equals(currency.Code, code, StringComparison.Ordinal));

    /// <summary>Whether <paramref name="code"/> is a supported ISO 4217 code (exact, upper case).</summary>
    public static bool IsSupported(string? code) => Find(code) is not null;

    /// <summary>
    /// <paramref name="code"/> when it is supported, else <see cref="DefaultCode"/>: how a page reads a DTO's currency,
    /// so a row from before amounts carried a currency, or an empty code, still renders.
    /// </summary>
    public static string CodeOrDefault(string? code) => IsSupported(code) ? code! : DefaultCode;

    /// <summary>
    /// <paramref name="amount"/> rounded to the minor units of the supported currency <paramref name="code"/>; throws for
    /// a code that is not supported, which a validator has already rejected.
    /// </summary>
    public static decimal Round(string code, decimal amount) =>
        (Find(code) ?? throw new ArgumentException($"'{code}' is not a supported currency.", nameof(code))).Round(amount);
}
