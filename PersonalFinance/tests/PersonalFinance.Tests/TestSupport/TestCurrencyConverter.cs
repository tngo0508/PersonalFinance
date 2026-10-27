using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;

namespace PersonalFinance.Tests;

/// <summary>
/// An <see cref="ICurrencyConverter"/> over an in-memory database for service tests, with a rate cache of its own so
/// tests never see each other's rates. The clock is <see cref="TimeProvider.System"/> unless a test pins it.
/// </summary>
internal static class TestCurrencyConverter
{
    /// <summary>A converter reading <paramref name="db"/>, with a fresh cache.</summary>
    public static ICurrencyConverter Create(AppDbContext db, TimeProvider? timeProvider = null) =>
        Create(db, NewCache(), timeProvider);

    /// <summary>A converter sharing <paramref name="cache"/>, for tests of caching and eviction.</summary>
    public static ICurrencyConverter Create(AppDbContext db, ExchangeRateCache cache, TimeProvider? timeProvider = null) =>
        new CurrencyConverter(db, cache, timeProvider ?? TimeProvider.System);

    /// <summary>An empty rate cache.</summary>
    public static ExchangeRateCache NewCache() => new(new MemoryCache(new MemoryCacheOptions()));
}
