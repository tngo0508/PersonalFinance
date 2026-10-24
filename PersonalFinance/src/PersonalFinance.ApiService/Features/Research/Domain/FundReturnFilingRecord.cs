namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One N-PORT filing whose head the fund returns refresh has read, kept so the filing is not fetched again.
/// </summary>
/// <param name="AccessionNumber">The filing's EDGAR accession number.</param>
/// <param name="Ticker">The Tracked Fund it was read for.</param>
/// <param name="FiledOn">The date it was filed: of two filings for one month, the later one's return is kept.</param>
/// <param name="ReportDate">The report's period date, or null when the filing gave no return for the fund.</param>
/// <param name="Returns">
/// The fund's three monthly returns in percent, oldest first and ending in the report month (a month reported as N/A
/// is null); null when EDGAR did not have the document, it was another series' report, or it had no return for the
/// fund's class.
/// </param>
internal sealed record FundReturnFilingRecord(string AccessionNumber, string Ticker, DateOnly FiledOn,
    DateOnly? ReportDate, List<decimal?>? Returns);
