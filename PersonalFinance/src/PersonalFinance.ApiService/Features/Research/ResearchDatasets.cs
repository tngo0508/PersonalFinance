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

    /// <summary>The World snapshot.</summary>
    public const string World = "world";
}
