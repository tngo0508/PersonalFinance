namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The one place amounts are converted into an Account's Reporting Currency (ADR 0016, localization spec §2.6).
/// Conversion happens only here, server-side: DTOs carry each amount with its currency plus its reporting amount, and
/// totals already converted; Web and JavaScript never multiply by a rate. A caller lists the amounts it will convert,
/// gets a <see cref="ReportingConversion"/> prepared with every rate those need (one batched read per currency and
/// date range, cached), and converts each amount with it.
/// </summary>
public interface ICurrencyConverter
{
    /// <summary>The Account's Reporting Currency code; <c>USD</c> when it has never chosen one.</summary>
    Task<string> GetReportingCurrencyAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A conversion into <paramref name="reportingCurrency"/> holding the rate for every amount in
    /// <paramref name="amounts"/>: a dated amount's own day (nearest prior rate), an undated one's latest rate. Amounts
    /// already in the Reporting Currency need no rate. Features pass the Account's Reporting Currency, always a
    /// supported one; the Currency Converter passes any convertible currency it converts into.
    /// </summary>
    /// <exception cref="ArgumentException">The currency is not convertible.</exception>
    Task<ReportingConversion> PrepareAsync(string reportingCurrency, IEnumerable<MoneyAt> amounts,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What a conversion needs to know about an amount before converting it: its currency and, for a dated item
/// (transaction, monthly line), its day. <c>null</c> is an undated balance (goal, parameter document, Dashboard "now"
/// figure), converted at the latest rate.
/// </summary>
public readonly record struct MoneyAt(string CurrencyCode, DateOnly? Date);
