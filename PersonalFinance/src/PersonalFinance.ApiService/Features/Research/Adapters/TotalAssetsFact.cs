namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// One filer's <c>us-gaap:Assets</c> in an SEC frame: its total assets (US$) as of <see cref="End"/>.
/// </summary>
/// <param name="Cik">The filer's SEC Central Index Key.</param>
/// <param name="End">The date the value is as of.</param>
/// <param name="Value">The total assets in US$.</param>
internal sealed record TotalAssetsFact(long Cik, DateOnly End, decimal Value);
