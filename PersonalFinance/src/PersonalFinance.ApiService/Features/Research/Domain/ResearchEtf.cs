namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One ETF of the ETFs dataset, as configured in <c>Research:Etfs</c>.
/// </summary>
internal sealed record ResearchEtf
{
    public ResearchEtf()
    {
    }

    public ResearchEtf(string ticker, string name, long? cik)
    {
        Ticker = ticker;
        Name = name;
        Cik = cik;
    }

    /// <summary>The ETF's ticker, e.g. <c>VTI</c>, looked up in SEC's fund ticker list for its series.</summary>
    public string Ticker { get; init; } = string.Empty;

    /// <summary>The ETF's name for display.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The trust's CIK, for a single-fund trust whose filings EDGAR lists under the trust rather than a series:
    /// SPY's trust is not in the fund ticker list, and QQQ's trust filed without a series until 2026. Null (left out
    /// of configuration) for an ETF whose series is found through the ticker list.
    /// </summary>
    public long? Cik { get; init; }
}
