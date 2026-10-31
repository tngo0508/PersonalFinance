using Microsoft.Extensions.Caching.Memory;

namespace PersonalFinance.Web.Services;

/// <summary>
/// <see cref="IResearchSnapshotCache"/> over <see cref="IMemoryCache"/>: a snapshot is kept for <see cref="Lifetime"/>.
/// </summary>
public sealed class ResearchSnapshotCache : IResearchSnapshotCache
{
    /// <summary>How long a snapshot is kept before it is read again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchSnapshotCache"/> class.
    /// </summary>
    /// <param name="cache">The memory cache holding the snapshots.</param>
    public ResearchSnapshotCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<T> GetOrReadAsync<T>(string dataset, Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken) where T : class
    {
        string key = $"research:{dataset}";
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        T snapshot = await read(cancellationToken);
        _cache.Set(key, snapshot, Lifetime);
        return snapshot;
    }
}
