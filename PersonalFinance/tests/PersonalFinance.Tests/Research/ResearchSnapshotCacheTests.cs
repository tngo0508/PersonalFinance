using System.Net;
using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Research;

public sealed class ResearchSnapshotCacheTests
{
    private sealed class FakeClock : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private static (ResearchSnapshotCache Cache, FakeClock Clock) Create()
    {
        var clock = new FakeClock();
        return (new ResearchSnapshotCache(new MemoryCache(new MemoryCacheOptions { Clock = clock })), clock);
    }

    [Fact]
    public async Task GetOrReadAsync_SecondCallWithinLifetime_DoesNotReadAgain()
    {
        (ResearchSnapshotCache cache, FakeClock _) = Create();
        int reads = 0;
        Task<string> Read(CancellationToken _)
        {
            reads++;
            return Task.FromResult(new string('x', 3));
        }

        string first = await cache.GetOrReadAsync("sectors", Read, CancellationToken.None);
        string second = await cache.GetOrReadAsync("sectors", Read, CancellationToken.None);

        Assert.Equal(1, reads);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetOrReadAsync_AfterLifetime_ReadsAgain()
    {
        (ResearchSnapshotCache cache, FakeClock clock) = Create();
        int reads = 0;
        Task<string> Read(CancellationToken _)
        {
            reads++;
            return Task.FromResult("v");
        }

        await cache.GetOrReadAsync("sectors", Read, CancellationToken.None);
        clock.UtcNow += ResearchSnapshotCache.Lifetime + TimeSpan.FromSeconds(1);
        await cache.GetOrReadAsync("sectors", Read, CancellationToken.None);

        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task GetOrReadAsync_WhenTheReadThrows_DoesNotCacheTheFailure()
    {
        (ResearchSnapshotCache cache, FakeClock _) = Create();
        int reads = 0;
        Task<string> Read(CancellationToken _)
        {
            reads++;
            if (reads == 1)
            {
                throw new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable);
            }
            return Task.FromResult("ok");
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetOrReadAsync("sectors", Read, CancellationToken.None));
        string result = await cache.GetOrReadAsync("sectors", Read, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task GetOrReadAsync_DifferentDatasets_AreCachedSeparately()
    {
        (ResearchSnapshotCache cache, FakeClock _) = Create();

        string a = await cache.GetOrReadAsync("a", _ => Task.FromResult("A"), CancellationToken.None);
        string b = await cache.GetOrReadAsync("b", _ => Task.FromResult("B"), CancellationToken.None);

        Assert.Equal("A", a);
        Assert.Equal("B", b);
    }

    [Fact]
    public async Task GetOrReadAsync_WhenTheValueIsRejectedByKeep_ReturnsItButReadsAgainNextTime()
    {
        (ResearchSnapshotCache cache, FakeClock _) = Create();
        int reads = 0;
        Task<string> Read(CancellationToken _)
        {
            reads++;
            return Task.FromResult(reads == 1 ? "partial" : "complete");
        }

        string first = await cache.GetOrReadAsync("a", Read, CancellationToken.None, keep: v => v == "complete");
        string second = await cache.GetOrReadAsync("a", Read, CancellationToken.None, keep: v => v == "complete");
        string third = await cache.GetOrReadAsync("a", Read, CancellationToken.None, keep: v => v == "complete");

        Assert.Equal("partial", first);
        Assert.Equal("complete", second);
        Assert.Equal("complete", third);
        Assert.Equal(2, reads);
    }
}
