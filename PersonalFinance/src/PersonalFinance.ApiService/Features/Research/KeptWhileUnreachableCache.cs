using Microsoft.Extensions.Caching.Memory;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// What the Companies tab built recently from SEC, by ticker, shared by every request (a singleton per
/// <typeparamref name="TValue"/>: Company Profiles, and press release lists). A value is <see cref="FreshFor"/> a day;
/// after that it is read again from SEC, but kept for <see cref="KeptFor"/> so it can still be served while SEC cannot
/// be reached. At most <see cref="MaxEntries"/> are kept, so memory stays bounded. Only trimmed DTOs are kept, never
/// SEC's documents.
/// </summary>
/// <typeparam name="TValue">What is kept for a ticker.</typeparam>
internal sealed class KeptWhileUnreachableCache<TValue> : IDisposable where TValue : class
{
    /// <summary>How long a value is served before it is read again.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);

    /// <summary>How long a value is kept to serve while SEC is unreachable.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(7);

    /// <summary>The most values kept at once.</summary>
    public const int MaxEntries = 2_000;

    private readonly MemoryCache _cache;
    private readonly TimeProvider _timeProvider;

    public KeptWhileUnreachableCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxEntries });
    }

    /// <summary>The value kept for <paramref name="ticker"/>, with whether it is still fresh; null when none is kept.</summary>
    public (TValue Value, bool Fresh)? Get(string ticker)
    {
        if (!_cache.TryGetValue(ticker, out Entry? entry) || entry is null)
        {
            return null;
        }

        TimeSpan age = _timeProvider.GetUtcNow() - entry.ReadAt;
        return age < KeptFor ? (entry.Value, age < FreshFor) : null;
    }

    /// <summary>Keeps <paramref name="value"/> for <paramref name="ticker"/>, read now.</summary>
    public void Set(string ticker, TValue value) =>
        _cache.Set(ticker, new Entry(value, _timeProvider.GetUtcNow()),
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = KeptFor });

    public void Dispose() => _cache.Dispose();

    private sealed record Entry(TValue Value, DateTimeOffset ReadAt);
}
