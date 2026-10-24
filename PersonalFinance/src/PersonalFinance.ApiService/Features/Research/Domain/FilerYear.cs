namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One filer's Public Float in one year: how a value <see cref="PublicFloatCleaner"/> drops is named, and how the
/// exclusion list in configuration (<c>Research:PublicFloatExclusions</c>) names a value to drop by hand.
/// </summary>
/// <param name="Cik">The filer's SEC Central Index Key.</param>
/// <param name="Year">The calendar year of the frames the value is in.</param>
internal sealed record FilerYear(long Cik, int Year);
