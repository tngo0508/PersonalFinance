using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// What a Company Profile lookup came to: an answer (a profile, a fund, or a ticker SEC does not list), too many
/// lookups by the Account, or SEC EDGAR unreachable with no kept profile to serve.
/// </summary>
/// <param name="Result">The answer, or null when <see cref="RateLimited"/> or <see cref="Unavailable"/>.</param>
/// <param name="RateLimited">True when the Account made too many lookups in the last minute.</param>
public sealed record CompanyLookupOutcome(CompanyLookupResultDto? Result, bool RateLimited = false)
{
    /// <summary>True when there is no answer because SEC EDGAR could not be reached.</summary>
    public bool Unavailable => Result is null && !RateLimited;

    /// <summary>An answer.</summary>
    public static CompanyLookupOutcome Answer(string kind, string ticker, CompanyProfileDto? profile = null) =>
        new(new CompanyLookupResultDto { Kind = kind, Ticker = ticker, Profile = profile });

    /// <summary>Too many lookups.</summary>
    public static CompanyLookupOutcome TooManyLookups() => new(null, RateLimited: true);

    /// <summary>SEC EDGAR unreachable.</summary>
    public static CompanyLookupOutcome SecUnavailable() => new(Result: null);
}
