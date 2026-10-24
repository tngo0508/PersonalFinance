using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// What a press releases request came to, as a Company Profile lookup does: an answer (a company's releases, a fund,
/// or a ticker SEC does not list), too many lookups by the Account, or SEC EDGAR unreachable with no kept list to serve.
/// </summary>
/// <param name="Result">The answer, or null when <see cref="RateLimited"/> or <see cref="Unavailable"/>.</param>
/// <param name="RateLimited">True when the Account made too many lookups in the last minute.</param>
public sealed record CompanyPressReleasesOutcome(CompanyPressReleasesResultDto? Result, bool RateLimited = false)
{
    /// <summary>True when there is no answer because SEC EDGAR could not be reached.</summary>
    public bool Unavailable => Result is null && !RateLimited;

    /// <summary>An answer.</summary>
    public static CompanyPressReleasesOutcome Answer(string kind, string ticker,
        List<CompanyPressReleaseDto>? releases = null) =>
        new(new CompanyPressReleasesResultDto { Kind = kind, Ticker = ticker, Releases = releases ?? [] });

    /// <summary>Too many lookups.</summary>
    public static CompanyPressReleasesOutcome TooManyLookups() => new(null, RateLimited: true);

    /// <summary>SEC EDGAR unreachable.</summary>
    public static CompanyPressReleasesOutcome SecUnavailable() => new(Result: null);
}
