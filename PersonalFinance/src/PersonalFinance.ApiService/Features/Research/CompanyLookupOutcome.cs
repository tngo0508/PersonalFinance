namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// What a company lookup request by ticker came to, for a Company Profile lookup
/// (<see cref="PersonalFinance.Shared.DTOs.Research.CompanyLookupResultDto"/>) or its press releases
/// (<see cref="PersonalFinance.Shared.DTOs.Research.CompanyPressReleasesResultDto"/>): an answer (a company's, a fund,
/// or a ticker SEC does not list), too many lookups by the Account, press releases switched off, or SEC EDGAR
/// unreachable with nothing kept to serve. Build one with named arguments: <c>new(result)</c>,
/// <c>new(null, RateLimited: true)</c>, <c>new(null, SwitchedOff: true)</c>, or <c>new(Result: null)</c> for
/// unavailable.
/// </summary>
/// <typeparam name="TResult">The answer's DTO.</typeparam>
/// <param name="Result">The answer, or null when <see cref="RateLimited"/>, <see cref="SwitchedOff"/> or
/// <see cref="Unavailable"/>.</param>
/// <param name="RateLimited">True when the Account made too many lookups in the last minute.</param>
/// <param name="SwitchedOff">True when what was asked for is switched off
/// (<see cref="ResearchSettings.PressReleasesEnabledKey"/>).</param>
public sealed record CompanyLookupOutcome<TResult>(TResult? Result, bool RateLimited = false, bool SwitchedOff = false)
    where TResult : class
{
    /// <summary>True when there is no answer because SEC EDGAR could not be reached.</summary>
    public bool Unavailable => Result is null && !RateLimited && !SwitchedOff;
}
