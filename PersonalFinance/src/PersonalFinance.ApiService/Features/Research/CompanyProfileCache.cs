using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The Company Profiles built recently, by ticker, shared by every request (a singleton). A profile is
/// <see cref="FreshFor"/> a day; after that it is rebuilt from SEC, but kept for <see cref="KeptFor"/> so it can still be
/// served while SEC cannot be reached. At most <see cref="MaxProfiles"/> are kept, so memory stays bounded. Only the
/// trimmed profile is kept, never SEC's documents.
/// </summary>
internal sealed class CompanyProfileCache : IDisposable
{
    /// <summary>How long a profile is served before it is rebuilt.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);

    /// <summary>How long a profile is kept to serve while SEC is unreachable.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(7);

    /// <summary>The most profiles kept at once.</summary>
    public const int MaxProfiles = 2_000;

    private readonly MemoryCache _cache;
    private readonly TimeProvider _timeProvider;

    public CompanyProfileCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxProfiles });
    }

    /// <summary>The profile kept for <paramref name="ticker"/>, with whether it is still fresh; null when none is kept.</summary>
    public (CompanyProfileDto Profile, bool Fresh)? Get(string ticker)
    {
        if (!_cache.TryGetValue(ticker, out Entry? entry) || entry is null)
        {
            return null;
        }

        TimeSpan age = _timeProvider.GetUtcNow() - entry.BuiltAt;
        return age < KeptFor ? (entry.Profile, age < FreshFor) : null;
    }

    /// <summary>Keeps <paramref name="profile"/> for <paramref name="ticker"/>, built now.</summary>
    public void Set(string ticker, CompanyProfileDto profile) =>
        _cache.Set(ticker, new Entry(profile, _timeProvider.GetUtcNow()),
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = KeptFor });

    public void Dispose() => _cache.Dispose();

    private sealed record Entry(CompanyProfileDto Profile, DateTimeOffset BuiltAt);
}
