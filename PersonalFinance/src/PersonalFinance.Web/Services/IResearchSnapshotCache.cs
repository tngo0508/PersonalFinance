namespace PersonalFinance.Web.Services;

/// <summary>
/// Holds the Research snapshots Web has read from ApiService, so a page view does not fetch them again each time.
/// </summary>
public interface IResearchSnapshotCache
{
    /// <summary>
    /// The cached snapshot of <paramref name="dataset"/>, or the result of <paramref name="read"/> (then cached) when
    /// there is none. A read that throws (503, unreachable, cancelled) propagates and nothing is cached.
    /// </summary>
    /// <typeparam name="T">The snapshot type.</typeparam>
    /// <param name="dataset">The dataset's name.</param>
    /// <param name="read">Reads the snapshot from ApiService.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    Task<T> GetOrReadAsync<T>(string dataset, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
        where T : class;
}
