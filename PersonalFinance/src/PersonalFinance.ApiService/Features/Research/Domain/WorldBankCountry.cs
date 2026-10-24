namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>A World Bank country or aggregate: its ISO 3166-1 alpha-3 (or World Bank) code and name.</summary>
internal sealed record WorldBankCountry(string Iso3, string Name, bool IsAggregate);
