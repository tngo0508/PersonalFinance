using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// What the Research datasets cover (<c>Research</c> in configuration).
/// </summary>
internal sealed class ResearchSettings
{
    /// <summary>The configuration section the settings are read from.</summary>
    public const string Section = "Research";

    /// <summary>
    /// The first year of the Sectors dataset (<c>Research:SectorsFirstYear</c>). SEC's Public Float frames are sparse
    /// before 2011.
    /// </summary>
    public int SectorsFirstYear { get; init; } = 2011;

    /// <summary>
    /// How many filers outside the largest 500 one refresh reads the SEC submissions of
    /// (<c>Research:SubmissionsPerRefresh</c>), largest first. Each one read is remembered, so the first refreshes
    /// build up the list over a few days while staying inside the job's time limit; until a filer is read, its share
    /// of "Other" counts as Unassigned.
    /// </summary>
    public int SubmissionsPerRefresh { get; init; } = 2_500;

    /// <summary>
    /// The filer years whose Public Float the Sectors snapshot leaves out by hand
    /// (<c>Research:PublicFloatExclusions</c>, a list of <c>{ "Cik": ..., "Year": ... }</c>).
    /// </summary>
    public IReadOnlyList<FilerYear> PublicFloatExclusions { get; init; } = [];

    /// <summary>
    /// The filer years whose Public Float the Sectors snapshot keeps although it is more than
    /// <see cref="PublicFloatCleaner.MaxTimesNearestOtherYear"/> times the filer's nearest other year
    /// (<c>Research:PublicFloatKeeps</c>, a list of <c>{ "Cik": ..., "Year": ... }</c>): real companies that grew
    /// that fast, such as a spin-off in its first full year. The other cleaning rules still apply to them.
    /// </summary>
    public IReadOnlyList<FilerYear> PublicFloatKeeps { get; init; } = [];

    /// <summary>
    /// The Tracked Funds of the funds dataset (<c>Research:Funds</c>, a list of
    /// <c>{ "Ticker", "Name", "Cik", "Role" }</c>), in the order the page lists them. ETFs and mutual funds alike; a
    /// fund added later is read back to the first quarter on the next refresh. With none configured the dataset is not
    /// built.
    /// </summary>
    public IReadOnlyList<TrackedFund> Funds { get; init; } = [];

    /// <summary>
    /// The first day of the first quarter of the funds dataset (<c>Research:FundsFirstQuarter</c>): 1 October 2019,
    /// the first quarter of public Form N-PORT reports.
    /// </summary>
    public DateOnly FundsFirstQuarter { get; init; } = new(2019, 10, 1);

    /// <summary>
    /// The first year the World dataset asks the World Bank for (<c>Research:WorldFirstYear</c>): 1975, where its
    /// market-cap series starts. Before 1990 only a few dozen countries report; the snapshot's periods begin at the
    /// first year any country has a value.
    /// </summary>
    public int WorldFirstYear { get; init; } = 1975;
}
