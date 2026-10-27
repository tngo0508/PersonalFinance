using PersonalFinance.Shared.DTOs.CurrencyConverter;

namespace PersonalFinance.ApiService.Features.CurrencyConverter;

/// <summary>
/// The Currency Converter's calculation (GLOSSARY.md): one amount between two Convertible Currencies, through the
/// shared <see cref="Infrastructure.ICurrencyConverter"/>. Stateless: nothing is saved.
/// </summary>
public interface ICurrencyConversionService
{
    /// <summary>
    /// Converts <paramref name="amount"/> of <paramref name="from"/> into <paramref name="to"/>, both convertible (the
    /// validator has checked them): the cross rate through USD at the latest rates, or each leg's nearest prior rate to
    /// <paramref name="date"/>; the amount rounded once to the To currency's minor units.
    /// </summary>
    Task<CurrencyConversionDto> ConvertAsync(decimal amount, string from, string to, DateOnly? date,
        CancellationToken cancellationToken = default);
}
