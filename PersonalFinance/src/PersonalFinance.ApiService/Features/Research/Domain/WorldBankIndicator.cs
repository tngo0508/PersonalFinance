namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>One indicator's observations, and the date the World Bank last updated it.</summary>
internal sealed record WorldBankIndicator(DateOnly? LastUpdated, IReadOnlyList<WorldBankObservation> Observations);
