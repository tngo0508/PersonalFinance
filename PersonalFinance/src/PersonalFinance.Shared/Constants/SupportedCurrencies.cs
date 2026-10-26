namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The currencies an amount may be in (ADR 0016, localization spec §2.5). Adding one is an entry here plus the rate
/// refresher quoting it; the schema does not change. No enum and no lookup table. Only USD is used until slice C1.
/// </summary>
public static class SupportedCurrencies
{
    /// <summary>US dollar: two minor-unit decimals.</summary>
    public static readonly SupportedCurrency Usd = new() { Code = "USD", DisplayName = "US dollar", MinorUnits = 2, Symbol = "$" };

    /// <summary>Vietnamese đồng: no minor units in use.</summary>
    public static readonly SupportedCurrency Vnd = new() { Code = "VND", DisplayName = "Vietnamese đồng", MinorUnits = 0, Symbol = "₫" };

    /// <summary>The currency every amount is in until amounts carry their own (slice C1).</summary>
    public static SupportedCurrency Default => Usd;

    /// <summary>Every supported currency.</summary>
    public static readonly IReadOnlyList<SupportedCurrency> All = [Usd, Vnd];

    /// <summary>The currency with ISO 4217 <paramref name="code"/> (exact, upper case); <c>null</c> when it is not supported.</summary>
    public static SupportedCurrency? Find(string? code) =>
        All.FirstOrDefault(currency => string.Equals(currency.Code, code, StringComparison.Ordinal));

    /// <summary>Whether <paramref name="code"/> is a supported ISO 4217 code (exact, upper case).</summary>
    public static bool IsSupported(string? code) => Find(code) is not null;
}
