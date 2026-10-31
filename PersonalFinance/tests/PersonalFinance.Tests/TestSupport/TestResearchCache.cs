using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>A Research snapshot cache of its own, so no test sees the snapshots another test read.</summary>
public static class TestResearchCache
{
    /// <summary>Creates an empty cache over a new <see cref="MemoryCache"/>.</summary>
    public static IResearchSnapshotCache Create() => new ResearchSnapshotCache(new MemoryCache(new MemoryCacheOptions()));
}
