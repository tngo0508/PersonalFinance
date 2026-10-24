namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// The parts of a Form N-PORT report the ETF snapshot reads: whose report it is, the date its holdings are as of,
/// and every holding.
/// </summary>
/// <param name="SeriesId">The series the report is for, or null for a fund filed without one (e.g. SPY's trust).</param>
/// <param name="ReportDate">The date the holdings are as of (<c>repPdDate</c>).</param>
/// <param name="NetAssets">The fund's net assets (US$).</param>
/// <param name="Holdings">Every investment the report lists, in the report's order.</param>
internal sealed record NportReport(string? SeriesId, DateOnly ReportDate, decimal NetAssets,
    IReadOnlyList<NportHolding> Holdings);
