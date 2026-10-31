namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// What the Research page's search box looks up, built from the funds, Sectors and World datasets so the page loads one
/// small list instead of each dataset: the Tracked Funds, the SEC industry offices, the SIC industries and the
/// countries. A part whose dataset has never loaded is empty.
/// </summary>
public sealed class ResearchSearchIndexDto
{
    /// <summary>The Tracked Funds, in the snapshot's order.</summary>
    public List<TrackedFundDto> Funds { get; set; } = [];

    /// <summary>The SEC industry offices, e.g. <c>Technology</c>, in the snapshot's order.</summary>
    public List<string> Offices { get; set; } = [];

    /// <summary>The SIC industries, each with its office, in the snapshot's order.</summary>
    public List<ResearchSearchIndustryDto> Industries { get; set; } = [];

    /// <summary>The names of the countries with a market cap in any year, in the snapshot's order.</summary>
    public List<string> Countries { get; set; } = [];
}

/// <summary>
/// A SIC industry of the search index.
/// </summary>
public sealed class ResearchSearchIndustryDto
{
    /// <summary>The SIC description for display, e.g. <c>Semiconductors &amp; Related Devices</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The name of the industry's SEC industry office, e.g. <c>Technology</c>.</summary>
    public string Office { get; set; } = string.Empty;
}
