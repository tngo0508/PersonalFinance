using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// <see cref="ICurrencyConverter"/> over the <see cref="ExchangeRate"/> table and <see cref="ExchangeRateCache"/>.
/// A cold read is one batched query per quote currency and date range, then one more for the rate before the range
/// when a day's nearest prior rate lies further back; the latest rate is one query per quote. The Account's Reporting
/// Currency is read once per request.
/// </summary>
internal sealed class CurrencyConverter : ICurrencyConverter
{
    /// <summary>How far before the earliest requested day the batched read starts, so a weekend or holiday gap is covered.</summary>
    internal const int LookBackDays = 14;

    private readonly AppDbContext _db;
    private readonly ExchangeRateCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, string> _reportingByUser = new(StringComparer.Ordinal);

    public CurrencyConverter(AppDbContext db, ExchangeRateCache cache, TimeProvider timeProvider)
    {
        _db = db;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetReportingCurrencyAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (_reportingByUser.TryGetValue(userId, out string? known))
        {
            return known;
        }

        string? stored = await _db.UserPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .Select(p => p.ReportingCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        // A currency dropped from the supported list later reads as the default, never as a broken page.
        string code = SupportedCurrencies.IsSupported(stored) ? stored! : SupportedCurrencies.DefaultCode;
        _reportingByUser[userId] = code;
        return code;
    }

    public async Task<ReportingConversion> PrepareAsync(string reportingCurrency, IEnumerable<MoneyAt> amounts,
        CancellationToken cancellationToken = default)
    {
        ConvertibleCurrency reporting = ConvertibleCurrencies.Find(reportingCurrency)
                                        ?? throw new ArgumentException(
                                            $"'{reportingCurrency}' is not a convertible currency.",
                                            nameof(reportingCurrency));
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var keys = new HashSet<RateKey>();
        foreach (MoneyAt amount in amounts)
        {
            if (string.Equals(amount.CurrencyCode, reporting.Code, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(amount.CurrencyCode, ExchangeRateCache.BaseCurrency, StringComparison.Ordinal))
            {
                keys.Add(new RateKey(amount.CurrencyCode, amount.Date));
            }

            if (!string.Equals(reporting.Code, ExchangeRateCache.BaseCurrency, StringComparison.Ordinal))
            {
                keys.Add(new RateKey(reporting.Code, amount.Date));
            }
        }

        var rates = new Dictionary<RateKey, ExchangeRateRow?>();
        foreach (IGrouping<string, RateKey> quote in keys.GroupBy(k => k.QuoteCurrency))
        {
            await LoadAsync(quote.Key, quote.ToList(), rates, cancellationToken);
        }

        return new ReportingConversion(reporting, today, rates);
    }

    /// <summary>Resolves each key of one quote currency from the cache, reading the table once for the rest.</summary>
    private async Task LoadAsync(string quote, List<RateKey> keys, Dictionary<RateKey, ExchangeRateRow?> rates,
        CancellationToken cancellationToken)
    {
        var missing = new List<RateKey>();
        foreach (RateKey key in keys)
        {
            if (_cache.TryGet(key, out ExchangeRateRow? cached))
            {
                rates[key] = cached;
            }
            else
            {
                missing.Add(key);
            }
        }

        if (missing.Any(k => k.Date is null))
        {
            Store(new RateKey(quote, null), await LatestAsync(quote, null, cancellationToken), rates);
        }

        List<DateOnly> days = missing.Where(k => k.Date is not null).Select(k => k.Date!.Value).Distinct().ToList();
        if (days.Count == 0)
        {
            return;
        }

        DateOnly first = days.Min();
        DateOnly from = first.AddDays(-LookBackDays);
        DateOnly to = days.Max();
        List<ExchangeRateRow> window = (await Rows(quote)
                .Where(r => r.Date >= from && r.Date <= to)
                .OrderBy(r => r.Date)
                .Select(r => new { r.Date, r.Rate, r.Source })
                .ToListAsync(cancellationToken))
            .Select(r => new ExchangeRateRow(r.Date, r.Rate, r.Source))
            .ToList();

        // The rate before the window, so a day whose nearest prior rate lies further back than the look-back still finds it.
        ExchangeRateRow? floor = window.Count == 0 || window[0].Date > first
            ? await LatestAsync(quote, from, cancellationToken)
            : null;

        foreach (DateOnly day in days)
        {
            ExchangeRateRow? nearestPrior = window.LastOrDefault(r => r.Date <= day) ?? floor;
            Store(new RateKey(quote, day), nearestPrior, rates);
        }
    }

    /// <summary>The most recent stored rate of <paramref name="quote"/>, before <paramref name="before"/> when given.</summary>
    private async Task<ExchangeRateRow?> LatestAsync(string quote, DateOnly? before, CancellationToken cancellationToken)
    {
        IQueryable<ExchangeRate> rows = Rows(quote);
        if (before is { } limit)
        {
            rows = rows.Where(r => r.Date < limit);
        }

        var latest = await rows
            .OrderByDescending(r => r.Date)
            .Select(r => new { r.Date, r.Rate, r.Source })
            .FirstOrDefaultAsync(cancellationToken);
        return latest is null ? null : new ExchangeRateRow(latest.Date, latest.Rate, latest.Source);
    }

    private void Store(RateKey key, ExchangeRateRow? row, Dictionary<RateKey, ExchangeRateRow?> rates)
    {
        _cache.Set(key, row);
        rates[key] = row;
    }

    private IQueryable<ExchangeRate> Rows(string quote) =>
        _db.ExchangeRates.AsNoTracking()
            .Where(r => r.BaseCurrency == ExchangeRateCache.BaseCurrency && r.QuoteCurrency == quote);
}
