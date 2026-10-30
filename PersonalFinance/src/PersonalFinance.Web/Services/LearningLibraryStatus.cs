using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Services;

/// <summary>Whether the Library has any Published Term Entry, which decides if the nav shows "Learning".</summary>
public interface ILearningLibraryStatus
{
    /// <summary><c>true</c> once a Term Entry is Published; <c>false</c> while the Library is empty or unreachable.</summary>
    Task<bool> HasEntriesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="ILearningLibraryStatus"/> from ApiService, cached for a few minutes because every page's nav asks.
/// Content changes only with a deploy, so a short cache is never stale for long.
/// </summary>
public sealed class LearningLibraryStatus : ILearningLibraryStatus
{
    private const string CacheKey = "learning-library-has-entries";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

    private readonly ILearningApi _learningApi;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LearningLibraryStatus> _logger;

    public LearningLibraryStatus(ILearningApi learningApi, IMemoryCache cache, ILogger<LearningLibraryStatus> logger)
    {
        _learningApi = learningApi;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> HasEntriesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out bool cached))
        {
            return cached;
        }

        try
        {
            LearningLibraryDto library = await _learningApi.GetLibraryAsync(AppLanguages.Default, cancellationToken);
            bool hasEntries = library.Topics.Any(t => t.Terms.Count > 0);
            _cache.Set(CacheKey, hasEntries, CacheFor);
            return hasEntries;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The nav must render whatever ApiService does; hide the entry and ask again soon. That includes the
            // HttpClient.Timeout a cold ApiService hits, which surfaces as a TaskCanceledException; only the
            // caller's own cancellation (the browser went away) propagates.
            _logger.LogWarning(ex, "Failed to read whether the Library has entries; hiding the Learning nav entry.");
            _cache.Set(CacheKey, false, RetryAfterFailure);
            return false;
        }
    }
}
