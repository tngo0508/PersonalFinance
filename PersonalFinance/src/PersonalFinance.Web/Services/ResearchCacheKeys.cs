namespace PersonalFinance.Web.Services;

/// <summary>
/// The names <see cref="IResearchSnapshotCache"/> keeps the Research snapshots under. Web-side: ApiService's own
/// dataset keys are internal to it. They are keys only; a log line's label is separate.
/// </summary>
public static class ResearchCacheKeys
{
    /// <summary>The Sectors snapshot.</summary>
    public const string Sectors = "sectors";

    /// <summary>The funds snapshot.</summary>
    public const string Funds = "funds";

    /// <summary>The World snapshot.</summary>
    public const string World = "world";

    /// <summary>The fund overlap snapshot.</summary>
    public const string FundOverlap = "fund-overlap";

    /// <summary>The fund returns snapshot.</summary>
    public const string FundReturns = "fund-returns";

    /// <summary>The macro snapshot.</summary>
    public const string Macro = "macro";

    /// <summary>The fund facts snapshot.</summary>
    public const string FundFacts = "fund-facts";

    /// <summary>The fund exposure snapshot.</summary>
    public const string FundExposure = "fund-exposure";

    /// <summary>The search index of the page's search box.</summary>
    public const string SearchIndex = "search-index";
}
