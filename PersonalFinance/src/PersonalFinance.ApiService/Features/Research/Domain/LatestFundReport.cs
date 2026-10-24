namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>One fund's latest Form N-PORT report, read whole for a dataset built from each fund's latest holdings.</summary>
/// <param name="Ticker">The fund's ticker.</param>
/// <param name="Accession">The report's EDGAR accession number.</param>
/// <param name="Report">The parsed report, with each holding's security key, country and currency in memory.</param>
internal sealed record LatestFundReport(string Ticker, string Accession, NportReport Report);

/// <summary>A Tracked Fund with its recent N-PORT filings, newest first.</summary>
/// <param name="Fund">The fund.</param>
/// <param name="SeriesId">The series its reports must belong to, or null when not known (a trust with no series).</param>
/// <param name="NewestFirst">Its recent filings, newest first; never empty.</param>
internal sealed record ListedFund(TrackedFund Fund, string? SeriesId, IReadOnlyList<Adapters.NportFilingRef> NewestFirst);
