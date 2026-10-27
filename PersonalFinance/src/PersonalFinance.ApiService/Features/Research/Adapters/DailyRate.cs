namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>One day's rate: units of the quote currency per one US dollar.</summary>
internal sealed record DailyRate(DateOnly Date, decimal Rate);
