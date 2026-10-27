using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Currency;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Converts amounts into one Reporting Currency with rates prepared by <see cref="ICurrencyConverter.PrepareAsync"/>
/// (ADR 0016, localization spec §2.6). USD is the only stored base, so another pair goes through USD. An amount already
/// in the Reporting Currency is returned as it is, with no lookup and no caption. A missing rate converts to
/// <c>null</c>, never to zero. Converted amounts keep their full precision; <see cref="Total"/> rounds once per sum.
/// <see cref="ToDto"/> reports the rates actually used, for the page's caption.
/// </summary>
public sealed class ReportingConversion
{
    /// <summary>A rate more than this many days older than the day it stands in for is shown as "rate as of {date}".</summary>
    public const int StaleAfterDays = 3;

    private readonly SupportedCurrency _reporting;
    private readonly DateOnly _today;
    private readonly IReadOnlyDictionary<RateKey, ExchangeRateRow?> _rates;
    private readonly Dictionary<string, RateUse> _uses = new(StringComparer.Ordinal);
    private bool _converted;
    private bool _unavailable;

    internal ReportingConversion(SupportedCurrency reporting, DateOnly today,
        IReadOnlyDictionary<RateKey, ExchangeRateRow?> rates)
    {
        _reporting = reporting;
        _today = today;
        _rates = rates;
    }

    /// <summary>The ISO 4217 code every converted amount is in.</summary>
    public string ReportingCurrency => _reporting.Code;

    /// <summary>Whether <paramref name="currencyCode"/> is the Reporting Currency, so amounts in it need no conversion.</summary>
    public bool IsReporting(string currencyCode) => string.Equals(currencyCode, _reporting.Code, StringComparison.Ordinal);

    /// <summary>
    /// <paramref name="amount"/> in the Reporting Currency, at the rate of <paramref name="date"/> (the latest when
    /// null); <c>null</c> when there is no rate at all. Unrounded.
    /// </summary>
    /// <exception cref="InvalidOperationException">The amount was not among those the conversion was prepared for.</exception>
    public decimal? Convert(decimal amount, string currencyCode, DateOnly? date)
    {
        if (IsReporting(currencyCode))
        {
            return amount;
        }

        _converted = true;
        decimal? usd = ToUsd(amount, currencyCode, date);
        decimal? result = usd is { } value ? FromUsd(value, date) : null;
        if (result is null)
        {
            _unavailable = true;
        }

        return result;
    }

    /// <summary>
    /// <see cref="Convert"/>, or the original amount when there is no rate: a total that cannot be converted is still
    /// shown, under an "unavailable" caption, never as zero.
    /// </summary>
    public decimal ConvertOrOriginal(decimal amount, string currencyCode, DateOnly? date) =>
        Convert(amount, currencyCode, date) ?? amount;

    /// <summary><paramref name="amount"/> rounded to the Reporting Currency's minor units, halves away from zero.</summary>
    public decimal Round(decimal amount) => _reporting.Round(amount);

    /// <summary>The sum of converted amounts, rounded once at the end (localization spec §2.5).</summary>
    public decimal Total(IEnumerable<decimal> reportingAmounts) => Round(reportingAmounts.Sum());

    /// <summary>The rates this conversion has used so far, for the page's caption.</summary>
    public ConversionDto ToDto() => new()
    {
        ReportingCurrency = _reporting.Code,
        Converted = _converted,
        Unavailable = _unavailable,
        Rates = _uses.Values
            .OrderBy(u => u.Quote, StringComparer.Ordinal)
            .Select(u => new ConversionRateDto
            {
                BaseCurrency = ExchangeRateCache.BaseCurrency,
                QuoteCurrency = u.Quote,
                Rate = u.Rate,
                Date = u.Date,
                IsStale = u.IsStale,
                IsDated = u.IsDated
            })
            .ToList()
    };

    private decimal? ToUsd(decimal amount, string currencyCode, DateOnly? date)
    {
        if (string.Equals(currencyCode, ExchangeRateCache.BaseCurrency, StringComparison.Ordinal))
        {
            return amount;
        }

        ExchangeRateRow? row = Lookup(currencyCode, date);
        if (row is null)
        {
            return null;
        }

        Use(currencyCode, row, date);
        return amount / row.Rate;
    }

    private decimal? FromUsd(decimal usd, DateOnly? date)
    {
        if (string.Equals(_reporting.Code, ExchangeRateCache.BaseCurrency, StringComparison.Ordinal))
        {
            return usd;
        }

        ExchangeRateRow? row = Lookup(_reporting.Code, date);
        if (row is null)
        {
            return null;
        }

        Use(_reporting.Code, row, date);
        return usd * row.Rate;
    }

    private ExchangeRateRow? Lookup(string quote, DateOnly? date)
    {
        if (!_rates.TryGetValue(new RateKey(quote, date), out ExchangeRateRow? row))
        {
            throw new InvalidOperationException(
                $"No rate was prepared for {quote} on {(date is { } day ? day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "the latest day")}; " +
                "pass every amount to ICurrencyConverter.PrepareAsync.");
        }

        return row;
    }

    /// <summary>Records a rate's use: the most recent day per quote, and whether any use was stale or dated.</summary>
    private void Use(string quote, ExchangeRateRow row, DateOnly? date)
    {
        bool stale = row.Date < (date ?? _today).AddDays(-StaleAfterDays);
        if (_uses.TryGetValue(quote, out RateUse? use))
        {
            use.IsStale |= stale;
            use.IsDated |= date is not null;
            if (row.Date > use.Date)
            {
                use.Date = row.Date;
                use.Rate = row.Rate;
            }

            return;
        }

        _uses[quote] = new RateUse(quote) { Date = row.Date, Rate = row.Rate, IsStale = stale, IsDated = date is not null };
    }

    private sealed class RateUse(string quote)
    {
        public string Quote { get; } = quote;
        public DateOnly Date { get; set; }
        public decimal Rate { get; set; }
        public bool IsStale { get; set; }
        public bool IsDated { get; set; }
    }
}

/// <summary>A prepared rate lookup: the quote currency and the day asked for (<c>null</c> for the latest).</summary>
public readonly record struct RateKey(string QuoteCurrency, DateOnly? Date);

/// <summary>A stored USD rate: units of the quote currency per one US dollar on <see cref="Date"/>.</summary>
public sealed record ExchangeRateRow(DateOnly Date, decimal Rate);
