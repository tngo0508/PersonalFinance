namespace PersonalFinance.Web.Services;

/// <summary>
/// Holds the Research snapshots Web has read from ApiService, so a page view does not fetch them again each time.
/// A returned snapshot is shared across requests and users: callers must treat it as read-only and never modify it.
/// </summary>
public interface IResearchSnapshotCache
{
    /// <summary>
    /// The cached snapshot of <paramref name="dataset"/>, or the result of <paramref name="read"/> (then cached) when
    /// there is none. A read that throws (503, unreachable, cancelled) propagates and nothing is cached. The returned
    /// snapshot is the instance shared by every request and user, so it must not be modified.
    /// </summary>
    /// <typeparam name="T">The snapshot type.</typeparam>
    /// <param name="dataset">The dataset's name.</param>
    /// <param name="read">Reads the snapshot from ApiService.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="keep">
    /// Whether a snapshot just read is worth keeping; one it rejects is returned but not cached, so the next call reads
    /// again. Omitted, every snapshot is kept.
    /// </param>
    /// <returns>The snapshot.</returns>
    Task<T> GetOrReadAsync<T>(string dataset, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken,
        Func<T, bool>? keep = null) where T : class;
}
