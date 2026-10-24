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
    /// The ETFs of the ETFs dataset (<c>Research:Etfs</c>, a list of <c>{ "Ticker", "Name", "Cik" }</c>), in the
    /// order the page lists them. With none configured the dataset is not built.
    /// </summary>
    public IReadOnlyList<ResearchEtf> Etfs { get; init; } = [];

    /// <summary>
    /// The first day of the first quarter of the ETFs dataset (<c>Research:EtfsFirstQuarter</c>): 1 October 2019,
    /// the first quarter of public Form N-PORT reports.
    /// </summary>
    public DateOnly EtfsFirstQuarter { get; init; } = new(2019, 10, 1);

    /// <summary>
    /// The first year of the World dataset (<c>Research:WorldFirstYear</c>). The World Bank's market-cap series starts
    /// in 1975, but before 1990 only a few dozen countries report.
    /// </summary>
    public int WorldFirstYear { get; init; } = 1990;
}
