namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// What a filer's SEC submissions say about it: the fields the Sectors snapshot groups and labels it by.
/// </summary>
/// <param name="Cik">The filer's SEC Central Index Key.</param>
/// <param name="Name">The filer's name as SEC reports it.</param>
/// <param name="Ticker">The filer's first ticker, or null when it has none.</param>
/// <param name="SicCode">The filer's four-digit SIC code, or null when SEC has none.</param>
/// <param name="SicDescription">The SIC code's description, e.g. <c>SEMICONDUCTORS &amp; RELATED DEVICES</c>.</param>
/// <param name="OwnerOrg">
/// The SEC industry office that reviews the filer's filings, e.g. <c>06 Technology</c>, or null when SEC has none.
/// </param>
internal sealed record SecFiler(long Cik, string Name, string? Ticker, string? SicCode, string? SicDescription,
    string? OwnerOrg);
