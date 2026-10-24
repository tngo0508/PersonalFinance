using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// <see cref="ICompanyProfileService"/> over SEC EDGAR. The ticker list, each built profile and each press release list
/// are cached (<see cref="CompanyTickerIndexCache"/>, <see cref="CompanyProfileCache"/>,
/// <see cref="CompanyPressReleaseCache"/>), so a repeat lookup within a day makes no SEC request, and a kept profile or
/// list is served when SEC cannot be reached.
/// </summary>
internal sealed class CompanyProfileService : ICompanyProfileService
{
    private readonly ISecCompanyClient _client;
    private readonly CompanyTickerIndexCache _index;
    private readonly CompanyProfileCache _profiles;
    private readonly CompanyPressReleaseCache _releases;
    private readonly IRateLimiterService _rateLimiter;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompanyProfileService> _logger;

    public CompanyProfileService(ISecCompanyClient client, CompanyTickerIndexCache index, CompanyProfileCache profiles,
        CompanyPressReleaseCache releases,
        [FromKeyedServices(CompanyLookupRateLimit.Key)]
        IRateLimiterService rateLimiter,
        ResearchSettings settings,
        TimeProvider timeProvider,
        ILogger<CompanyProfileService> logger)
    {
        _client = client;
        _index = index;
        _profiles = profiles;
        _releases = releases;
        _rateLimiter = rateLimiter;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool PressReleasesEnabled => _settings.PressReleasesEnabled;

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

    /// <inheritdoc />
    public async Task<CompanyPressReleasesOutcome> GetPressReleasesAsync(string userId, string ticker,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryAllow(userId))
        {
            return CompanyPressReleasesOutcome.TooManyLookups();
        }

        CompanyTickerIndex? index = await _index.GetAsync(_client, cancellationToken);
        if (index is null)
        {
            return CompanyPressReleasesOutcome.SecUnavailable();
        }

        if (index.IsFund(ticker))
        {
            return CompanyPressReleasesOutcome.Answer(CompanyLookupKinds.Fund, ticker);
        }

        if (index.Find(ticker) is not { } listing)
        {
            return CompanyPressReleasesOutcome.Answer(CompanyLookupKinds.NotFound, ticker);
        }

        (List<CompanyPressReleaseDto> Releases, bool Fresh)? kept = _releases.Get(ticker);
        if (kept is { Fresh: true })
        {
            return CompanyPressReleasesOutcome.Answer(CompanyLookupKinds.Profile, ticker, kept.Value.Releases);
        }

        try
        {
            SecCompanySubmissions? submissions =
                await _client.GetCompanySubmissionsAsync(listing.Cik, cancellationToken);
            if (submissions is null)
            {
                return CompanyPressReleasesOutcome.Answer(CompanyLookupKinds.NotFound, ticker);
            }

            List<CompanyPressReleaseDto> releases = await ReadPressReleasesAsync(submissions, cancellationToken);
            _releases.Set(ticker, releases);
            return CompanyPressReleasesOutcome.Answer(CompanyLookupKinds.Profile, ticker, releases);
        }
        // As for a profile: any failure to read or understand SEC's documents serves the kept list or answers 503,
        // never a 500; only the caller cancelling propagates.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not read SEC EDGAR for a company's press releases; {Fallback}",
                kept is null ? "no kept list to serve" : "serving the kept list");
            return kept is { } stale
                ? CompanyPressReleasesOutcome.Answer(CompanyLookupKinds.Profile, ticker, stale.Releases)
                : CompanyPressReleasesOutcome.SecUnavailable();
        }
    }

    /// <summary>
    /// Reads the exhibit 99 documents of the company's recent 8-Ks, newest filing first, until
    /// <see cref="PressReleaseBuilder.MaxReleases"/> are found, so the SEC requests stay bounded.
    /// </summary>
    private async Task<List<CompanyPressReleaseDto>> ReadPressReleasesAsync(SecCompanySubmissions submissions,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var releases = new List<CompanyPressReleaseDto>();
        foreach (SecFilingEntry filing in PressReleaseBuilder.Filings(submissions, today))
        {
            IReadOnlyList<string> files =
                await _client.GetPressReleaseFileNamesAsync(submissions.Cik, filing.AccessionNumber, cancellationToken);
            foreach (string file in files)
            {
                string? head = PressReleaseBuilder.IsReadable(file)
                    ? await _client.GetDocumentHeadAsync(submissions.Cik, filing.AccessionNumber, file,
                        cancellationToken)
                    : null;
                releases.Add(PressReleaseBuilder.Release(submissions.Cik, filing, file, head));
                if (releases.Count == PressReleaseBuilder.MaxReleases)
                {
                    return releases;
                }
            }
        }

        return releases;
    }
}
