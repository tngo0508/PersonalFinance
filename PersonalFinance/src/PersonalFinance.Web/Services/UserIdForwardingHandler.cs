using System.Net.Http.Headers;
using System.Security.Claims;

namespace PersonalFinance.Web.Services;

/// <summary>
/// HTTP message handler that forwards the authenticated user's identity to downstream API calls.
/// Adds an <c>X-User-Id</c> header so the ApiService can identify the user without requiring
/// a separate JWT authentication layer between the two services.
/// </summary>
public sealed class UserIdForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserIdForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name
                   ?? "anonymous";

        request.Headers.Add("X-User-Id", userId);

        var response = await base.SendAsync(request, cancellationToken);
        return response;
    }
}
