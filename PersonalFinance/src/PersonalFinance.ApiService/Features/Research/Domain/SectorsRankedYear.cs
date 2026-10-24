namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One year of Public Float, ranked: the largest filers, largest first, and the rest.
/// </summary>
/// <param name="Year">The calendar year.</param>
/// <param name="Ranked">The largest <see cref="SectorsSnapshotBuilder.RankedPerPeriod"/> filers, largest first.</param>
/// <param name="Others">Every other filer, largest first.</param>
internal sealed record SectorsRankedYear(int Year, List<PublicFloatFact> Ranked, List<PublicFloatFact> Others);
