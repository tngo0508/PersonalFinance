namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Spaces the requests sent to SEC EDGAR at least <c>1 / MaxRequestsPerSecond</c> apart, across every
/// <see cref="SecEdgarClient"/> in the process (it is registered as a singleton).
/// </summary>
internal sealed class SecRequestGate : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    public SecRequestGate(SecEdgarSettings settings, TimeProvider timeProvider)
    {
        _interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, settings.MaxRequestsPerSecond));
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Waits until the next request may be sent, then reserves that slot.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            TimeSpan wait = _nextAllowed - _timeProvider.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _timeProvider, cancellationToken);
            }

            _nextAllowed = _timeProvider.GetUtcNow() + _interval;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();
}
