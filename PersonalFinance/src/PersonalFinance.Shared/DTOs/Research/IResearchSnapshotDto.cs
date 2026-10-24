namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// What every Research dataset's snapshot has: the date it is as of, its period labels, and one entry per period.
/// </summary>
/// <typeparam name="TPeriod">The dataset's per-period DTO.</typeparam>
public interface IResearchSnapshotDto<TPeriod>
{
    /// <summary>The date the data is as of ("Data as of" on the page).</summary>
    DateOnly AsOf { get; }

    /// <summary>The period labels, oldest first; <see cref="Values"/> follows this order.</summary>
    List<string> Periods { get; }

    /// <summary>One entry per period, in the order of <see cref="Periods"/>.</summary>
    List<TPeriod> Values { get; }
}
