using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// An N-PORT filing the funds refresh has read, kept so each filing is fetched once: a past quarter's report does not
/// change, and an amendment is a filing of its own.
/// </summary>
/// <param name="AccessionNumber">EDGAR's accession number of the filing.</param>
/// <param name="Ticker">The Tracked Fund the filing was read for.</param>
/// <param name="FiledOn">The filing date; of two reports for the same date, the later filed (an amendment) wins.</param>
/// <param name="Holdings">
/// The report summarised for the snapshot, or null when the filing has nothing to show: EDGAR did not have its
/// document, or it is another series' report.
/// </param>
internal sealed record FundFilingRecord(string AccessionNumber, string Ticker, DateOnly FiledOn, FundHoldingsDto? Holdings);
