namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The answer to a press releases request by ticker: a company's press releases, a fund, or a ticker SEC does not
/// list, as a Company Profile lookup answers.
/// </summary>
public sealed class CompanyPressReleasesResultDto
{
    /// <summary>
    /// One of <see cref="CompanyLookupKinds"/>: <see cref="CompanyLookupKinds.Profile"/> for a company, whose press
    /// releases <see cref="Releases"/> holds.
    /// </summary>
    public string Kind { get; set; } = CompanyLookupKinds.NotFound;

    /// <summary>The ticker that was looked up, in upper case.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// The company's press releases of the last 12 months, newest first, at most 10; empty for a fund, an unlisted
    /// ticker, or a company that attached none.
    /// </summary>
    public List<CompanyPressReleaseDto> Releases { get; set; } = [];
}
