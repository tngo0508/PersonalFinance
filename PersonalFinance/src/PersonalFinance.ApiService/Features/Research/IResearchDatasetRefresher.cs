namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds one Research dataset from its public source and stores it. <see cref="ResearchService"/> runs each one on
/// its own, in registration order, so one source failing never holds back another.
/// </summary>
internal interface IResearchDatasetRefresher
{
    /// <summary>The dataset this refresher builds (<see cref="ResearchDatasets"/>).</summary>
    string Dataset { get; }

    /// <summary>The source the dataset is built from.</summary>
    ResearchSources Source { get; }

    /// <summary>False when configuration gives the dataset nothing to build (e.g. no funds listed), so it is skipped.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Reads the source, builds the snapshot and stores it. Throws when the source fails or has nothing, so the
    /// previous snapshot is kept.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}
