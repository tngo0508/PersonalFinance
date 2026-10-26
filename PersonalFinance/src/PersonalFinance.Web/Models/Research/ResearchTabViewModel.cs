using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// One Research tab: its dataset, or its Unavailable State when the dataset has never loaded, could not be read, or
/// has no period.
/// </summary>
/// <typeparam name="TSnapshot">The dataset's snapshot DTO.</typeparam>
/// <typeparam name="TPeriod">The snapshot's per-period DTO.</typeparam>
public sealed class ResearchTabViewModel<TSnapshot, TPeriod>
    where TSnapshot : class, IResearchSnapshotDto<TPeriod>
    where TPeriod : class
{
    /// <summary>A tab over <paramref name="snapshot"/>, null when it could not be loaded.</summary>
    /// <param name="snapshot">The dataset, or null.</param>
    /// <param name="pageTitle">The Unavailable State's page title, the page's title in the request's Language.</param>
    /// <param name="unavailableHeading">The Unavailable State's heading, e.g. "We couldn't load the World data".</param>
    public ResearchTabViewModel(TSnapshot? snapshot, string pageTitle, string unavailableHeading)
    {
        Snapshot = snapshot;
        UnavailableState = new UnavailableStateViewModel
        {
            PageTitle = pageTitle,
            Heading = unavailableHeading,
            RetryController = "Research",
        };
    }

    /// <summary>The snapshot, or null when the tab is unavailable.</summary>
    public TSnapshot? Snapshot { get; }

    /// <summary>True when the tab shows the Unavailable State.</summary>
    public bool Unavailable => Snapshot is null || Snapshot.Values.Count == 0;

    /// <summary>The tab's Unavailable State.</summary>
    public UnavailableStateViewModel UnavailableState { get; }

    /// <summary>The date the data is as of ("Data as of").</summary>
    public DateOnly? AsOf => Snapshot?.AsOf;

    /// <summary>The latest period of the data, which the tab opens on.</summary>
    public TPeriod? LatestPeriod => Snapshot?.Values.LastOrDefault();
}
