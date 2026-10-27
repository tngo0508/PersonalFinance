using PersonalFinance.Shared.DTOs.CurrencyConverter;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Currency Converter: a stateless calculation over the stored exchange rates.
/// </summary>
public interface ICurrencyConverterApi
{
    /// <summary>
    /// Converts <paramref name="amount"/> of <paramref name="from"/> into <paramref name="to"/>, at the rate of
    /// <paramref name="date"/> or the latest. A null amount is left out of the query, and the ApiService rejects it.
    /// </summary>
    [Get("/api/CurrencyConverter")]
    Task<CurrencyConversionDto> ConvertAsync([Query] decimal? amount, [Query] string from, [Query] string to,
        [Query(Format = "yyyy-MM-dd")] DateOnly? date, CancellationToken cancellationToken = default);
}
