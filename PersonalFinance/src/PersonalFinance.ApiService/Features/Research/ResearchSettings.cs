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
}
