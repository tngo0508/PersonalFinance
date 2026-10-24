namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// An issuer name that matches a known SEC filer although SEC's company list does not give it under that name
/// (<c>Research:IssuerCikOverrides</c>): a company renamed, re-formed under a new CIK, or delisted since its funds
/// reported it. Matched like any name (<see cref="IssuerName"/>), so punctuation and the legal form do not matter.
/// </summary>
/// <param name="Name">The issuer's name as funds report it, e.g. <c>Exxon Mobil Corp</c>.</param>
/// <param name="Cik">The SEC Central Index Key of the filer it is.</param>
internal sealed record IssuerCikOverride(string Name, long Cik);
