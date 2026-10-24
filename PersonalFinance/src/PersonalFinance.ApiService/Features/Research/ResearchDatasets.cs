namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The keys of the stored Research datasets (<c>ResearchSnapshot.Dataset</c>): the three the page shows, and the two
/// caches of SEC reads a refresh keeps so it does not read them again.
/// </summary>
internal static class ResearchDatasets
{
    /// <summary>The Sectors snapshot.</summary>
    public const string Sectors = "sectors";

    /// <summary>
    /// The SEC submissions read for the Sectors snapshot, kept so each refresh reads only the filers it has not read
    /// before (and the latest year's largest). Not served to the page.
    /// </summary>
    public const string SecFilers = "sec-filers";

    /// <summary>
    /// The funds snapshot. The key keeps its first name, "etfs", so the snapshot already stored stays in use.
    /// </summary>
    public const string Funds = "etfs";

    /// <summary>
    /// The N-PORT filings read for the funds snapshot, kept so each refresh fetches only the filings it has not read
    /// before. Not served to the page.
    /// </summary>
    /// <remarks>The key keeps its first name, so the filings already read are not fetched again.</remarks>
    public const string FundFilings = "etf-filings";

    /// <summary>
    /// The fund overlap snapshot: every pair of Tracked Funds' overlap by weight, from each fund's latest report.
    /// </summary>
    public const string FundOverlap = "fund-overlap";

    /// <summary>
    /// The fund returns snapshot: each Tracked Fund's monthly total returns and the figures computed from them per
    /// window.
    /// </summary>
    public const string FundReturns = "fund-returns";

    /// <summary>
    /// The N-PORT filings whose returns the fund returns refresh has read, kept so each is fetched only once. Not served
    /// to the page.
    /// </summary>
    public const string FundReturnFilings = "fund-return-filings";

    /// <summary>
    /// The fund exposure snapshot: each Tracked Fund's share of net assets by issuer country and currency, from its
    /// latest report.
    /// </summary>
    public const string FundExposure = "fund-exposure";

    /// <summary>
    /// The fund industries snapshot: each Tracked Fund's share of net assets by SEC industry office and SIC industry,
    /// and the rest by issuer country, bond issuer type and cash, from its latest report.
    /// </summary>
    public const string FundIndustries = "fund-industries";

    /// <summary>
    /// The SEC submissions the fund industries refresh has read for holdings the Sectors snapshot has not, kept so each
    /// is read only once. Not served to the page.
    /// </summary>
    public const string FundIndustryFilers = "fund-industry-filers";

    /// <summary>The fund facts snapshot: each Tracked Fund's net assets and expense ratio.</summary>
    public const string FundFacts = "fund-facts";

    /// <summary>The macro snapshot: monthly CPI-U, effective federal funds rate and 3-month Treasury bill rate.</summary>
    public const string Macro = "macro";

    /// <summary>The World snapshot.</summary>
    public const string World = "world";
}
