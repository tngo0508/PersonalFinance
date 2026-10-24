using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// <see cref="ICompanyProfileService"/> over SEC EDGAR. The ticker list, each built profile and each press release list
/// are cached (<see cref="CompanyTickerIndexCache"/> and a <see cref="KeptWhileUnreachableCache{TValue}"/> each), so a
/// repeat lookup within a day makes no SEC request, and a kept profile or list is served when SEC cannot be reached.
/// </summary>
internal sealed class CompanyProfileService : ICompanyProfileService
{
    private readonly ISecCompanyClient _client;
    private readonly CompanyTickerIndexCache _index;
    private readonly KeptWhileUnreachableCache<CompanyProfileDto> _profiles;
    private readonly KeptWhileUnreachableCache<List<CompanyPressReleaseDto>> _releases;
    private readonly IRateLimiterService _rateLimiter;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompanyProfileService> _logger;

    public CompanyProfileService(ISecCompanyClient client, CompanyTickerIndexCache index,
        KeptWhileUnreachableCache<CompanyProfileDto> profiles,
        KeptWhileUnreachableCache<List<CompanyPressReleaseDto>> releases,
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
    public async Task<List<CompanySuggestionDto>?> SuggestAsync(string query,
        CancellationToken cancellationToken = default) =>
        (await _index.GetAsync(_client, cancellationToken))?.Suggest(query);

    /// <inheritdoc />
    public Task<CompanyLookupOutcome<CompanyLookupResultDto>> LookUpAsync(string userId, string ticker,
        CancellationToken cancellationToken = default) =>
        ReadThroughAsync(userId, ticker, _profiles,
            (listing, submissions, ct) => ReadProfileAsync(ticker, listing, submissions, ct),
            (kind, profile) => new CompanyLookupResultDto
            {
                Kind = kind,
                Ticker = ticker,
                Profile = profile,
                PressReleasesAvailable = profile is not null && _settings.PressReleasesEnabled,
            },
            "a Company Profile", cancellationToken);

    /// <inheritdoc />
    public Task<CompanyLookupOutcome<CompanyPressReleasesResultDto>> GetPressReleasesAsync(string userId,
        string ticker, CancellationToken cancellationToken = default) =>
        _settings.PressReleasesEnabled
            ? ReadThroughAsync(userId, ticker, _releases, ReadPressReleasesAsync,
                (kind, releases) => new CompanyPressReleasesResultDto
                    { Kind = kind, Ticker = ticker, Releases = releases ?? [] },
                "a company's press releases", cancellationToken)
            : Task.FromResult(new CompanyLookupOutcome<CompanyPressReleasesResultDto>(null, SwitchedOff: true));

    /// <summary>
    /// The steps a Companies tab request by ticker shares, whatever it reads: count it toward the Account's lookup
    /// limit; answer a fund or a ticker SEC does not list from the ticker list; serve a fresh value from
    /// <paramref name="cache"/>; else read the company's submissions and <paramref name="read"/> from them, keeping
    /// the result. When SEC cannot be read, the kept value is served, else the outcome is unavailable.
    /// </summary>
    /// <param name="userId">The Account the request counts toward.</param>
    /// <param name="ticker">The ticker, as SEC lists it.</param>
    /// <param name="cache">What was read recently, by ticker.</param>
    /// <param name="read">Reads the value from SEC, given the company's listing and submissions.</param>
    /// <param name="answer">The answer's DTO for a kind of <see cref="CompanyLookupKinds"/> and the value, if any.</param>
    /// <param name="what">What is read, for the log.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    private async Task<CompanyLookupOutcome<TResult>> ReadThroughAsync<TValue, TResult>(string userId, string ticker,
        KeptWhileUnreachableCache<TValue> cache,
        Func<SecCompanyListing, SecCompanySubmissions, CancellationToken, Task<TValue>> read,
        Func<string, TValue?, TResult> answer, string what, CancellationToken cancellationToken)
        where TValue : class
        where TResult : class
    {
        if (!_rateLimiter.TryAllow(userId))
        {
            return new(null, RateLimited: true);
        }

        CompanyTickerIndex? index = await _index.GetAsync(_client, cancellationToken);
        if (index is null)
        {
            return new(Result: null);
        }

        if (index.IsFund(ticker))
        {
            return new(answer(CompanyLookupKinds.Fund, null));
        }

        if (index.Find(ticker) is not { } listing)
        {
            return new(answer(CompanyLookupKinds.NotFound, null));
        }

        (TValue Value, bool Fresh)? kept = cache.Get(ticker);
        if (kept is { Fresh: true })
        {
            return new(answer(CompanyLookupKinds.Profile, kept.Value.Value));
        }

        try
        {
            SecCompanySubmissions? submissions =
                await _client.GetCompanySubmissionsAsync(listing.Cik, cancellationToken);
            if (submissions is null)
            {
                return new(answer(CompanyLookupKinds.NotFound, null));
            }

            TValue value = await read(listing, submissions, cancellationToken);
            cache.Set(ticker, value);
            return new(answer(CompanyLookupKinds.Profile, value));
        }
        // Any failure to read or understand SEC's documents (unreachable, a timeout, a shape the parser does not
        // expect) serves the kept value or answers 503, never a 500; only the caller cancelling propagates.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not read SEC EDGAR for {What}; {Fallback}", what,
                kept is null ? "nothing kept to serve" : "serving the kept copy");
            return kept is { } stale
                ? new(answer(CompanyLookupKinds.Profile, stale.Value))
                : new(Result: null);
        }
    }

    /// <summary>Builds the profile of <paramref name="ticker"/>'s company from its submissions and facts.</summary>
    private async Task<CompanyProfileDto> ReadProfileAsync(string ticker, SecCompanyListing listing,
        SecCompanySubmissions submissions, CancellationToken cancellationToken)
    {
        SecCompanyFacts? facts = await _client.GetCompanyFactsAsync(listing.Cik, cancellationToken);
        CompanyProfileDto profile = CompanyProfileBuilder.Build(ticker, submissions, facts);
        profile.Exchange ??= listing.Exchange;
        return profile;
    }

    /// <summary>
    /// Reads the exhibit 99 documents of the company's recent 8-Ks, newest filing first, until
    /// <see cref="PressReleaseBuilder.MaxReleases"/> are found, so the SEC requests stay bounded.
    /// </summary>
    private async Task<List<CompanyPressReleaseDto>> ReadPressReleasesAsync(SecCompanyListing listing,
        SecCompanySubmissions submissions, CancellationToken cancellationToken)
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
                releases.Add(PressReleaseBuilder.BuildRelease(submissions.Cik, filing, file, head));
                if (releases.Count == PressReleaseBuilder.MaxReleases)
                {
                    return releases;
                }
            }
        }

        return releases;
    }
}
