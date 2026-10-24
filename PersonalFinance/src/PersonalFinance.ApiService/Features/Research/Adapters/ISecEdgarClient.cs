using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads public data from SEC EDGAR (<c>data.sec.gov</c>).
/// </summary>
internal interface ISecEdgarClient
{
    /// <summary>
    /// Reads the <c>dei:EntityPublicFloat</c> instant frame of one calendar quarter (<c>CY{year}Q{quarter}I</c>): one
    /// value per filer whose reported date falls in that quarter. A frame SEC does not have (404) is empty; any other
    /// failure throws, so a refresh never builds from partial data.
    /// </summary>
    Task<IReadOnlyList<PublicFloatFact>> GetPublicFloatFrameAsync(int year, int quarter, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the <c>us-gaap:Assets</c> instant frame of one calendar quarter (<c>CY{year}Q{quarter}I</c>), which the
    /// cleaning of Public Float checks values against. Fails as <see cref="GetPublicFloatFrameAsync"/> does.
    /// </summary>
    Task<IReadOnlyList<TotalAssetsFact>> GetTotalAssetsFrameAsync(int year, int quarter, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one filer's submissions (<c>submissions/CIK{cik}.json</c>) for its name, ticker, SIC code and SEC
    /// industry office. Null when SEC has no submissions for the filer (404); any other failure throws.
    /// </summary>
    Task<SecFiler?> GetFilerAsync(long cik, CancellationToken cancellationToken);

    /// <summary>
    /// Reads SEC's fund ticker list (<c>www.sec.gov/files/company_tickers_mf.json</c>): every mutual fund and ETF share
    /// class with its series and trust. Any failure throws.
    /// </summary>
    Task<IReadOnlyList<SecFundSeries>> GetFundSeriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists the Form N-PORT filings (<c>NPORT-P</c> and amendments) EDGAR holds under a filer: a trust's CIK, or a
    /// series id such as <c>S000002848</c>. Newest first, back to <paramref name="filedFrom"/>. A filer EDGAR does not
    /// know (404) has none; any other failure throws.
    /// </summary>
    Task<IReadOnlyList<NportFilingRef>> GetNportFilingsAsync(string filer, DateOnly filedFrom,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one N-PORT filing's primary document. Null when EDGAR does not have it (404); any other failure throws,
    /// as does a document that cannot be parsed.
    /// </summary>
    Task<NportReport?> GetNportReportAsync(NportFilingRef filing, CancellationToken cancellationToken);

    /// <summary>
    /// Reads only the head of an N-PORT filing's primary document: its series, report date and monthly total returns
    /// (Item B.5, which comes before the holdings). Null when EDGAR does not have the document.
    /// </summary>
    Task<NportReturns?> GetNportReturnsAsync(NportFilingRef filing, CancellationToken cancellationToken);
}
