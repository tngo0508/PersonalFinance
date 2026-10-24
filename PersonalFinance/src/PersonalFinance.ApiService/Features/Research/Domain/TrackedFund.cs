namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One Tracked Fund (an ETF or a mutual fund) of the funds dataset, as configured in <c>Research:Funds</c>.
/// </summary>
internal sealed record TrackedFund
{
    public TrackedFund()
    {
    }

    public TrackedFund(string ticker, string name, long? cik, string? role = null)
    {
        Ticker = ticker;
        Name = name;
        Cik = cik;
        Role = role;
    }

    /// <summary>The fund's ticker, e.g. <c>VTI</c>, looked up in SEC's fund ticker list for its series.</summary>
    public string Ticker { get; init; } = string.Empty;

    /// <summary>The fund's name for display.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The fund's asset-class role in the list, e.g. <c>US broad</c>, <c>International</c> or <c>Bonds</c>, which the
    /// Fund Basket picker groups funds by. Null when configuration gives none.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// The trust's CIK, for a single-fund trust whose filings EDGAR lists under the trust rather than a series:
    /// SPY's trust is not in the fund ticker list, and QQQ's trust filed without a series until 2026. Null (left out
    /// of configuration) for a fund whose series is found through the ticker list.
    /// </summary>
    public long? Cik { get; init; }
}
