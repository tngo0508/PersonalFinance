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
}
