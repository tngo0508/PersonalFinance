namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// A Form N-PORT filing as EDGAR lists it: what to fetch, before its document has been read.
/// </summary>
/// <param name="AccessionNumber">EDGAR's accession number, e.g. <c>0000884394-26-000001</c>.</param>
/// <param name="FiledOn">The filing date.</param>
/// <param name="FormType"><c>NPORT-P</c>, or <c>NPORT-P/A</c> for an amendment.</param>
/// <param name="DocumentUrl">The filing's <c>primary_doc.xml</c> under <c>https://www.sec.gov/Archives/</c>.</param>
internal sealed record NportFilingRef(string AccessionNumber, DateOnly FiledOn, string FormType, string DocumentUrl);
