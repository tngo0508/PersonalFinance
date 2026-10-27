using FluentValidation;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The currency rule every money request shares (ADR 0016, localization spec §2.5): a request may name the currency of
/// its amounts, and when it does the code must be one of <see cref="SupportedCurrencies"/>. No code means the stored
/// row keeps its currency, or a new row is <see cref="SupportedCurrencies.Default"/>.
/// </summary>
internal static class CurrencyCodeRules
{
    /// <summary>The English message of every Feature's unsupported-currency key.</summary>
    public const string UnsupportedMessage = "Choose a supported currency.";

    /// <summary>Rejects a code that is not supported, under the Feature's <paramref name="key"/>.</summary>
    public static IRuleBuilderOptions<T, string?> SupportedCurrency<T>(this IRuleBuilder<T, string?> rule, string key) =>
        rule.Must(code => code is null || SupportedCurrencies.IsSupported(code))
            .WithErrorCode(key)
            .WithMessage(UnsupportedMessage);

    /// <summary>
    /// The currency a write stores: the requested code, else the row's stored one, else the default. The requested code
    /// has already passed <see cref="SupportedCurrency{T}"/>.
    /// </summary>
    public static string Resolve(string? requested, string? stored = null) =>
        requested ?? stored ?? SupportedCurrencies.DefaultCode;
}
