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
    /// The filer years whose Public Float the Sectors snapshot leaves out by hand
    /// (<c>Research:PublicFloatExclusions</c>, a list of <c>{ "Cik": ..., "Year": ... }</c>).
    /// </summary>
    public IReadOnlyList<FilerYear> PublicFloatExclusions { get; init; } = [];
}
