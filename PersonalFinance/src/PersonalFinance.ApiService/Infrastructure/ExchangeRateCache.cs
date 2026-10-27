using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The in-memory cache of resolved exchange rates (localization spec §2.6): one entry per quote currency and day
/// (the nearest prior stored rate for that day, or none), plus one per quote for the latest rate. Entries live an
/// hour, and <see cref="Evict"/> drops a quote's entries as soon as the rate refresh has written new rows, so a day
/// that had only an older rate standing in for it picks up its own.
/// </summary>
internal sealed class ExchangeRateCache
{
    /// <summary>The base of every stored rate; the inverse is computed when it is needed.</summary>
    public const string BaseCurrency = SupportedCurrencies.DefaultCode;

    /// <summary>How long an entry is trusted when nothing evicts it: the refresh job runs in its own process.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _evictions = new(StringComparer.Ordinal);

    public ExchangeRateCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>The cached resolution of <paramref name="key"/>; the row is null when the day has no rate at all.</summary>
    public bool TryGet(RateKey key, out ExchangeRateRow? row)
    {
        if (_cache.TryGetValue(Name(key), out CachedRate? cached) && cached is not null)
        {
            row = cached.Row;
            return true;
        }

        row = null;
        return false;
    }

    /// <summary>Caches the resolution of <paramref name="key"/>, a missing rate included.</summary>
    public void Set(RateKey key, ExchangeRateRow? row)
    {
        var options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime };
        options.AddExpirationToken(new CancellationChangeToken(TokenFor(key.QuoteCurrency).Token));
        _cache.Set(Name(key), new CachedRate(row), options);
    }

    /// <summary>Drops every entry of <paramref name="quoteCurrency"/>: called after the rate refresh writes.</summary>
    public void Evict(string quoteCurrency)
    {
        if (_evictions.TryRemove(quoteCurrency, out CancellationTokenSource? source))
        {
            source.Cancel();
            source.Dispose();
        }
    }

    private CancellationTokenSource TokenFor(string quoteCurrency) =>
        _evictions.GetOrAdd(quoteCurrency, _ => new CancellationTokenSource());

    private static string Name(RateKey key) =>
        $"fx:{key.QuoteCurrency}:{(key.Date is { } day ? day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "latest")}";

    private sealed record CachedRate(ExchangeRateRow? Row);
}
