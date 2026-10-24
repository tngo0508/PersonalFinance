using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads what SEC EDGAR publishes about one company, for the Companies tab. Implemented by
/// <see cref="SecEdgarClient"/> over HTTP, and by <see cref="FixtureSecCompanyClient"/> for E2E runs that cannot
/// reach SEC.
/// </summary>
internal interface ISecCompanyClient
{
    /// <summary>
    /// Reads SEC's company ticker list (<c>www.sec.gov/files/company_tickers_exchange.json</c>). Any failure throws.
    /// </summary>
    Task<IReadOnlyList<SecCompanyListing>> GetCompanyTickersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the tickers of every mutual fund and ETF share class (<c>company_tickers_mf.json</c>). Any failure throws.
    /// </summary>
    Task<IReadOnlyList<string>> GetFundTickersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads one company's submissions (<c>submissions/CIK{cik}.json</c>): its identity and recent filings. Null when
    /// SEC has none (404); any other failure throws.
    /// </summary>
    Task<SecCompanySubmissions?> GetCompanySubmissionsAsync(long cik, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one company's XBRL facts (<c>api/xbrl/companyfacts/CIK{cik}.json</c>), keeping only the concepts of
    /// <see cref="CompanyProfileConcepts"/>. Null when SEC has none (404); any other failure throws.
    /// </summary>
    Task<SecCompanyFacts?> GetCompanyFactsAsync(long cik, CancellationToken cancellationToken);
}
