namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// What a company's SEC submissions say about it: its identity and its most recent filings.
/// </summary>
/// <param name="Cik">The company's SEC Central Index Key.</param>
/// <param name="Name">The company's name as SEC reports it.</param>
/// <param name="Tickers">The company's tickers, in SEC's order.</param>
/// <param name="Exchanges">The exchange of each ticker, in the order of <paramref name="Tickers"/>; null where none.</param>
/// <param name="SicCode">The four-digit SIC code, or null.</param>
/// <param name="SicDescription">The SIC code's description, or null.</param>
/// <param name="OwnerOrg">The SEC industry office, e.g. <c>06 Technology</c>, or null.</param>
/// <param name="StateOfIncorporation">The state or country of incorporation, or null.</param>
/// <param name="FiscalYearEnd">The fiscal year end as <c>MMdd</c>, e.g. <c>0630</c>, or null.</param>
/// <param name="RecentFilings">The company's recent filings, newest first.</param>
internal sealed record SecCompanySubmissions(
    long Cik,
    string Name,
    IReadOnlyList<string> Tickers,
    IReadOnlyList<string?> Exchanges,
    string? SicCode,
    string? SicDescription,
    string? OwnerOrg,
    string? StateOfIncorporation,
    string? FiscalYearEnd,
    IReadOnlyList<SecFilingEntry> RecentFilings);
