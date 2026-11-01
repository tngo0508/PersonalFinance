using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The company lookup's <see cref="CompanyTickerIndex"/>, shared by every request (a singleton). It is read from SEC at
/// most once per <see cref="FreshFor"/>; when a re-read fails, the previous index keeps being served, so suggestions
/// survive an SEC outage. The Tracked Funds' tickers are counted as funds, as SEC's fund list leaves some out.
/// </summary>
internal sealed class CompanyTickerIndexCache : IDisposable
{
    /// <summary>How long an index is served before it is read again.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompanyTickerIndexCache> _logger;
    private CompanyTickerIndex? _index;
    private DateTimeOffset _readAt;

    public CompanyTickerIndexCache(ResearchSettings settings, TimeProvider timeProvider,
        ILogger<CompanyTickerIndexCache> logger)
    {
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// The index, read through <paramref name="client"/> when there is none or it is older than
    /// <see cref="FreshFor"/>. Null only when it has never been read and cannot be now.
    /// </summary>
    public async Task<CompanyTickerIndex?> GetAsync(ISecCompanyClient client, CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return _index;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return _index;
            }

            IReadOnlyList<SecCompanyListing> listings = await client.GetCompanyTickersAsync(cancellationToken);
            IReadOnlyList<string> funds = await client.GetFundTickersAsync(cancellationToken);
            _index = new CompanyTickerIndex(listings, funds.Concat(_settings.Funds.Select(e => e.Ticker)));
            _readAt = _timeProvider.GetUtcNow();
            return _index;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not read SEC's company ticker list; {Fallback}",
                _index is null ? "no list to fall back on" : "serving the previous list");
            return _index;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsFresh() => _index is not null && _timeProvider.GetUtcNow() - _readAt < FreshFor;

    public void Dispose() => _lock.Dispose();
}
