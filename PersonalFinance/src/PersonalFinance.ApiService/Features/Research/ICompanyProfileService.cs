using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The Research company lookup: search suggestions and Company Profiles (GLOSSARY.md), read from SEC EDGAR, for
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
    /// company's profile, a fund, or a ticker SEC does not list. Counts toward the Account's lookup limit. A profile
    /// says whether its press releases can be read (<see cref="CompanyLookupResultDto.PressReleasesAvailable"/>).
    /// </summary>
    Task<CompanyLookupOutcome<CompanyLookupResultDto>> LookUpAsync(string userId, string ticker,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The press releases of <paramref name="ticker"/> for <paramref name="userId"/>: the announcements the company
    /// attached to its 8-K filings in the last 12 months, newest first, at most 10; or a fund, or a ticker SEC does
    /// not list. Counts toward the Account's lookup limit. Switched off
    /// (<see cref="CompanyLookupOutcome{TResult}.SwitchedOff"/>) without reading or counting anything when
    /// <see cref="ResearchSettings.PressReleasesEnabledKey"/> is false.
    /// </summary>
    Task<CompanyLookupOutcome<CompanyPressReleasesResultDto>> GetPressReleasesAsync(string userId, string ticker,
        CancellationToken cancellationToken = default);
}
