using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// <see cref="ICompanyProfileService"/> over SEC EDGAR. The ticker list and each built profile are cached
/// (<see cref="CompanyTickerIndexCache"/>, <see cref="CompanyProfileCache"/>), so a repeat lookup within a day makes no
/// SEC request, and a kept profile is served when SEC cannot be reached.
/// </summary>
internal sealed class CompanyProfileService : ICompanyProfileService
{
    private readonly ISecCompanyClient _client;
    private readonly CompanyTickerIndexCache _index;
    private readonly CompanyProfileCache _profiles;
    private readonly IRateLimiterService _rateLimiter;
    private readonly ILogger<CompanyProfileService> _logger;

    public CompanyProfileService(ISecCompanyClient client, CompanyTickerIndexCache index, CompanyProfileCache profiles,
        [FromKeyedServices(CompanyLookupRateLimit.Key)]
        IRateLimiterService rateLimiter,
        ILogger<CompanyProfileService> logger)
    {
        _client = client;
        _index = index;
        _profiles = profiles;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<CompanySuggestionDto>?> SuggestAsync(string query,
        CancellationToken cancellationToken = default) =>
        (await _index.GetAsync(_client, cancellationToken))?.Suggest(query);

    /// <inheritdoc />
    public async Task<CompanyLookupOutcome> LookUpAsync(string userId, string ticker,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryAllow(userId))
        {
            return CompanyLookupOutcome.TooManyLookups();
        }

        CompanyTickerIndex? index = await _index.GetAsync(_client, cancellationToken);
        if (index is null)
        {
            return CompanyLookupOutcome.SecUnavailable();
        }

        if (index.IsFund(ticker))
        {
            return CompanyLookupOutcome.Answer(CompanyLookupKinds.Fund, ticker);
        }

        if (index.Find(ticker) is not { } listing)
        {
            return CompanyLookupOutcome.Answer(CompanyLookupKinds.NotFound, ticker);
        }

        (CompanyProfileDto Profile, bool Fresh)? kept = _profiles.Get(ticker);
        if (kept is { Fresh: true })
        {
            return CompanyLookupOutcome.Answer(CompanyLookupKinds.Profile, ticker, kept.Value.Profile);
        }

        try
        {
            SecCompanySubmissions? submissions =
                await _client.GetCompanySubmissionsAsync(listing.Cik, cancellationToken);
            if (submissions is null)
            {
                return CompanyLookupOutcome.Answer(CompanyLookupKinds.NotFound, ticker);
            }

            SecCompanyFacts? facts = await _client.GetCompanyFactsAsync(listing.Cik, cancellationToken);
            CompanyProfileDto profile = CompanyProfileBuilder.Build(ticker, submissions, facts);
            profile.Exchange ??= listing.Exchange;
            _profiles.Set(ticker, profile);
            return CompanyLookupOutcome.Answer(CompanyLookupKinds.Profile, ticker, profile);
        }
        // Any failure to read or understand SEC's documents (unreachable, a timeout, a shape the parser does not
        // expect) serves the kept profile or answers 503, never a 500; only the caller cancelling propagates.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not read SEC EDGAR for a Company Profile; {Fallback}",
                kept is null ? "no kept profile to serve" : "serving the kept profile");
            return kept is { } stale
                ? CompanyLookupOutcome.Answer(CompanyLookupKinds.Profile, ticker, stale.Profile)
                : CompanyLookupOutcome.SecUnavailable();
        }
    }
}
