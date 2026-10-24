using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The press release lists read recently, by ticker, shared by every request (a singleton), with the rules of
/// <see cref="CompanyProfileCache"/>: a list is <see cref="FreshFor"/> a day; after that it is read again from SEC, but
/// kept for <see cref="KeptFor"/> so it can still be served while SEC cannot be reached. At most <see cref="MaxLists"/>
/// are kept, so memory stays bounded. Only the trimmed releases are kept, never SEC's documents.
/// </summary>
internal sealed class CompanyPressReleaseCache : IDisposable
{
    /// <summary>How long a list is served before it is read again.</summary>
    public static readonly TimeSpan FreshFor = CompanyProfileCache.FreshFor;

    /// <summary>How long a list is kept to serve while SEC is unreachable.</summary>
    public static readonly TimeSpan KeptFor = CompanyProfileCache.KeptFor;

    /// <summary>The most lists kept at once.</summary>
    public const int MaxLists = CompanyProfileCache.MaxProfiles;

    private readonly MemoryCache _cache;
    private readonly TimeProvider _timeProvider;

    public CompanyPressReleaseCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxLists });
    }

    /// <summary>The list kept for <paramref name="ticker"/>, with whether it is still fresh; null when none is kept.</summary>
    public (List<CompanyPressReleaseDto> Releases, bool Fresh)? Get(string ticker)
    {
        if (!_cache.TryGetValue(ticker, out Entry? entry) || entry is null)
        {
            return null;
        }

        TimeSpan age = _timeProvider.GetUtcNow() - entry.ReadAt;
        return age < KeptFor ? (entry.Releases, age < FreshFor) : null;
    }

    /// <summary>Keeps <paramref name="releases"/> for <paramref name="ticker"/>, read now.</summary>
    public void Set(string ticker, List<CompanyPressReleaseDto> releases) =>
        _cache.Set(ticker, new Entry(releases, _timeProvider.GetUtcNow()),
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = KeptFor });

    public void Dispose() => _cache.Dispose();

    private sealed record Entry(List<CompanyPressReleaseDto> Releases, DateTimeOffset ReadAt);
}
