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

    /// <summary>
    /// Reads one filing's document list from its index headers
    /// (<c>Archives/edgar/data/{cik}/{accession without dashes}/{accession}-index-headers.html</c>) and returns the file
    /// names of its exhibit 99 documents (<c>EX-99</c>, <c>EX-99.1</c>, ...), in the order the filing lists them.
    /// Empty when SEC has no such filing (404); any other failure throws.
    /// </summary>
    Task<IReadOnlyList<string>> GetPressReleaseFileNamesAsync(long cik, string accessionNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the head of one document of a filing (<c>Archives/edgar/data/{cik}/{accession without dashes}/{file}</c>):
    /// at most the first <see cref="SecEdgarClient.DocumentHeadBytes"/> bytes, as text. Null when SEC has no such
    /// document (404); any other failure throws.
    /// </summary>
    Task<string?> GetDocumentHeadAsync(long cik, string accessionNumber, string fileName,
        CancellationToken cancellationToken);
}
