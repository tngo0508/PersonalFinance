namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// One filer's <c>dei:EntityPublicFloat</c> in an SEC frame: the market value (US$) of its shares held by
/// non-affiliates, as of <see cref="End"/>.
/// </summary>
/// <param name="Cik">The filer's SEC Central Index Key.</param>
/// <param name="EntityName">The filer's name as SEC reports it.</param>
/// <param name="End">The date the value is as of.</param>
/// <param name="Value">The Public Float in US$.</param>
internal sealed record PublicFloatFact(long Cik, string EntityName, DateOnly End, decimal Value);
