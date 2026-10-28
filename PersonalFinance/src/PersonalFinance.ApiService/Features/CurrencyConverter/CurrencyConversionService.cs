using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.CurrencyConverter;

namespace PersonalFinance.ApiService.Features.CurrencyConverter;

/// <summary>
/// <see cref="ICurrencyConversionService"/> over <see cref="ICurrencyConverter"/>: it prepares a conversion into the
/// To currency for the one amount and asks it for the effective rate. No rate lookup of its own.
/// </summary>
internal sealed class CurrencyConversionService : ICurrencyConversionService
{
    private readonly ICurrencyConverter _converter;

    public CurrencyConversionService(ICurrencyConverter converter)
    {
        _converter = converter;
    }

    public async Task<CurrencyConversionDto> ConvertAsync(decimal amount, string from, string to, DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var result = new CurrencyConversionDto { Amount = amount, From = from, To = to, Date = date };

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            result.ConvertedAmount = amount;
            result.Rate = 1m;
            return result;
        }

        ReportingConversion conversion =
            await _converter.PrepareAsync(to, [new MoneyAt(from, date)], cancellationToken);
        RateQuote? quote = conversion.Quote(from, date);
        if (quote is null)
        {
            result.Unavailable = true;
            return result;
        }

        result.ConvertedAmount = conversion.Round(amount * quote.Rate);
        result.Rate = quote.Rate;
        result.RateDate = quote.RateDate;
        result.Sources = [.. quote.Sources];
        result.IsStale = quote.IsStale;
        return result;
    }
}
