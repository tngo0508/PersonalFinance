using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Fills the <see cref="ExchangeRate"/> table (ADR 0016, localization spec §2.6): for each
/// <see cref="ConvertibleCurrencies">convertible currency</see> but the US dollar, the USD rate of every day from the day after the latest Frankfurter rate (or <see cref="FirstDate"/>) to
/// today. It runs with the Research datasets in the daily refresh job. When Frankfurter cannot be read, the latest rate
/// comes from the currency-api instead, so the table still gains a recent day; the next Frankfurter read fills the days
/// in between. No source needs a secret. Rows are only added, never rewritten. After writing, it evicts the quote's
/// cached rates so a conversion in this process picks the new rows up at once.
/// </summary>
internal sealed class FxRatesRefresher : IResearchDatasetRefresher
{
    /// <summary>The base of every stored rate; the inverse is computed when it is needed.</summary>
    public const string BaseCurrency = SupportedCurrencies.DefaultCode;

    /// <summary>The <see cref="ExchangeRate.Source"/> of a Frankfurter rate.</summary>
    public const string FrankfurterSource = "frankfurter";

    /// <summary>The <see cref="ExchangeRate.Source"/> of a currency-api rate.</summary>
    public const string CurrencyApiSource = "currency-api";

    /// <summary>
    /// The first day a quote with no rates yet is filled from, so older Budget Tracker and spreadsheet rows have a rate.
    /// </summary>
    internal static readonly DateOnly FirstDate = ConvertibleCurrencies.FirstRateDate;

    private readonly AppDbContext _db;
    private readonly IFrankfurterClient _frankfurter;
    private readonly ICurrencyApiClient _currencyApi;
    private readonly ExchangeRateCache _rateCache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FxRatesRefresher> _logger;

    public FxRatesRefresher(AppDbContext db, IFrankfurterClient frankfurter, ICurrencyApiClient currencyApi,
        ExchangeRateCache rateCache, TimeProvider timeProvider, ILogger<FxRatesRefresher> logger)
    {
        _db = db;
        _frankfurter = frankfurter;
        _currencyApi = currencyApi;
        _rateCache = rateCache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.FxRates;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.CurrencyRates;

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <summary>
    /// Adds the missing days of each quote currency. Throws when neither source answers a rate for one, which leaves
    /// the rates already stored as they are.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        foreach (ConvertibleCurrency quote in ConvertibleCurrencies.All.Where(c => c.Code != BaseCurrency))
        {
            DateOnly? latest = await _db.ExchangeRates
                .Where(r => r.BaseCurrency == BaseCurrency && r.QuoteCurrency == quote.Code
                            && r.Source == FrankfurterSource)
                .MaxAsync(r => (DateOnly?)r.Date, cancellationToken);
            DateOnly from = latest?.AddDays(1) ?? FirstDate;
            if (from > today)
            {
                continue;
            }

            (IReadOnlyList<DailyRate> rates, string source) = await ReadAsync(quote.Code, from, today, cancellationToken);
            await StoreAsync(quote.Code, rates, source, cancellationToken);
            _rateCache.Evict(quote.Code);
        }
    }

    /// <summary>Frankfurter's rates for the days, or, when it cannot be read or has none, the currency-api's latest.</summary>
    private async Task<(IReadOnlyList<DailyRate> Rates, string Source)> ReadAsync(string quote, DateOnly from,
        DateOnly to, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<DailyRate> rates = await _frankfurter.GetUsdRatesAsync(quote, from, to, cancellationToken);
            if (rates.Count > 0)
            {
                return (rates, FrankfurterSource);
            }

            _logger.LogWarning("Frankfurter has no USD to {Quote} rate from {From} to {To}; reading the currency-api",
                quote, from, to);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Could not read Frankfurter's USD to {Quote} rates; reading the currency-api", quote);
        }

        DailyRate latest = await _currencyApi.GetLatestUsdRateAsync(quote, cancellationToken);
        return ([latest], CurrencyApiSource);
    }

    /// <summary>Adds each rate whose day has none stored yet; a day already stored keeps its rate.</summary>
    private async Task StoreAsync(string quote, IReadOnlyList<DailyRate> rates, string source,
        CancellationToken cancellationToken)
    {
        DateOnly first = rates.Min(r => r.Date);
        DateOnly last = rates.Max(r => r.Date);
        HashSet<DateOnly> stored = (await _db.ExchangeRates
                .Where(r => r.BaseCurrency == BaseCurrency && r.QuoteCurrency == quote && r.Date >= first && r.Date <= last)
                .Select(r => r.Date)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (DailyRate rate in rates.GroupBy(r => r.Date).Select(day => day.Last()))
        {
            if (stored.Add(rate.Date))
            {
                _db.ExchangeRates.Add(new ExchangeRate
                {
                    BaseCurrency = BaseCurrency,
                    QuoteCurrency = quote,
                    Date = rate.Date,
                    Rate = decimal.Round(rate.Rate, 8, MidpointRounding.AwayFromZero),
                    Source = source
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
