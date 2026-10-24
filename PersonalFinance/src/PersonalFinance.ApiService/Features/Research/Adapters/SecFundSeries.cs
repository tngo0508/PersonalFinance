namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// One share class in SEC's fund ticker list (<c>company_tickers_mf.json</c>): the ticker, the series it belongs to
/// and the trust that files for the series.
/// </summary>
/// <param name="Cik">The trust's SEC Central Index Key.</param>
/// <param name="SeriesId">The series id, e.g. <c>S000002848</c>, which EDGAR lists the series' filings under.</param>
/// <param name="Ticker">The share class's ticker, e.g. <c>VTI</c>.</param>
internal sealed record SecFundSeries(long Cik, string SeriesId, string Ticker);
