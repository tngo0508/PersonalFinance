using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The Companies tab of Research: search suggestions and Company Profiles (GLOSSARY.md), read from SEC EDGAR, for
/// information only. Nothing about the Account is stored.
/// </summary>
public interface ICompanyProfileService
{
    /// <summary>
    /// Up to eight companies matching <paramref name="query"/> by ticker or name. Null when SEC's ticker list has never
    /// been read and cannot be now.
    /// </summary>
    Task<List<CompanySuggestionDto>?> SuggestAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up <paramref name="ticker"/> (as SEC lists it, e.g. <c>BRK-B</c>) for <paramref name="userId"/>: the
    /// company's profile, a fund, or a ticker SEC does not list. Counts toward the Account's lookup limit.
    /// </summary>
    Task<CompanyLookupOutcome> LookUpAsync(string userId, string ticker, CancellationToken cancellationToken = default);
}
