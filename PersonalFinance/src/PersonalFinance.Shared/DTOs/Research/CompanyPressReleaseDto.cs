namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// One press release on a Company Profile: an announcement the company attached to an 8-K filing as exhibit 99, never
/// third-party coverage.
/// </summary>
public sealed class CompanyPressReleaseDto
{
    /// <summary>
    /// The release's headline as read from the start of its document, or, when none could be read, the 8-K's events in
    /// plain words joined with " · " (see <see cref="HeadlineRead"/>).
    /// </summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>The date the 8-K carrying the release was filed.</summary>
    public DateOnly FiledOn { get; set; }

    /// <summary>The events the 8-K reports in plain words, e.g. <c>Results of operations</c>.</summary>
    public List<string> Events { get; set; } = [];

    /// <summary>The release document on sec.gov.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>True when <see cref="Headline"/> was read from the document; false when it fell back to the events.</summary>
    public bool HeadlineRead { get; set; }
}
