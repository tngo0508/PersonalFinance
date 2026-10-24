namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>What <see cref="PublicFloatCleaner.Clean"/> kept, per year, and the filer years it dropped.</summary>
/// <param name="Kept">Per year, each filer's latest value that was kept.</param>
/// <param name="Dropped">The filer years whose value was dropped, by year then CIK.</param>
internal sealed record PublicFloatCleaning(
    IReadOnlyDictionary<int, IReadOnlyList<PublicFloatFact>> Kept,
    IReadOnlyList<FilerYear> Dropped);
