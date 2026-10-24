namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// The head of a Form N-PORT report: its series, its report date and the fund's monthly total returns (Item B.5).
/// </summary>
/// <param name="SeriesId">The series the report belongs to, or null when it has none (a unit investment trust).</param>
/// <param name="ReportDate">The date the report is as of (its period date): the month of its third return.</param>
/// <param name="Classes">Each share class's three monthly returns, as reported.</param>
internal sealed record NportReturns(string? SeriesId, DateOnly ReportDate, IReadOnlyList<NportClassReturns> Classes)
{
    /// <summary>
    /// The returns of the share class with <paramref name="classId"/>; when there is no such class, the report's only
    /// returns (a fund with one class, or a trust that reports no class). Null when neither is there.
    /// </summary>
    public NportClassReturns? For(string? classId) =>
        Classes.FirstOrDefault(c => classId is not null && string.Equals(c.ClassId, classId, StringComparison.OrdinalIgnoreCase))
        ?? (Classes.Count == 1 ? Classes[0] : Classes.SingleOrDefault(c => c.ClassId is null));
}

/// <summary>
/// One share class's monthly total returns from Item B.5: the NAV return in percent (distributions reinvested, net of
/// expenses) for each of the three months a report covers, oldest first. A month reported as N/A is null.
/// </summary>
/// <param name="ClassId">The class id, e.g. <c>C000007808</c>, or null when the report gives none.</param>
/// <param name="Month1">The return two months before the report month.</param>
/// <param name="Month2">The return the month before the report month.</param>
/// <param name="Month3">The return in the report month.</param>
internal sealed record NportClassReturns(string? ClassId, decimal? Month1, decimal? Month2, decimal? Month3);
