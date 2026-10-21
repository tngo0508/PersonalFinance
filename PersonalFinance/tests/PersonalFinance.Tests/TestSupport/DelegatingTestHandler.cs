namespace PersonalFinance.Tests;

/// <summary>
/// Simple mock delegating handler for testing HttpClient responses without external networks.
/// </summary>
public class DelegatingTestHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public DelegatingTestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}
