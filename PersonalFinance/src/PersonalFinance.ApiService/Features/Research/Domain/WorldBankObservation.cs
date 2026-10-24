namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>One country's value of an indicator in one year; null when the World Bank has none.</summary>
internal sealed record WorldBankObservation(string Iso3, int Year, decimal? Value);
