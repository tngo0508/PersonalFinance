namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// A Research dataset as last built, or "never loaded" when no refresh has built it yet.
/// </summary>
/// <typeparam name="TSnapshot">The dataset's Shared DTO.</typeparam>
public sealed class ResearchSnapshotResult<TSnapshot> where TSnapshot : class
{
    /// <summary>A loaded snapshot, or "never loaded" when <paramref name="snapshot"/> is null.</summary>
    public ResearchSnapshotResult(TSnapshot? snapshot)
    {
        Snapshot = snapshot;
    }

    /// <summary>The snapshot, or null when the dataset has never loaded.</summary>
    public TSnapshot? Snapshot { get; }
}
