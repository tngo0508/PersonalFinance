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
}
