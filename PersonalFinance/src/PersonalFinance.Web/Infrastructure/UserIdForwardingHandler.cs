using System.Net.Http.Headers;
using System.Security.Claims;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// HTTP message handler that forwards the authenticated user's identity to downstream API calls.
/// Adds an <c>X-User-Id</c> header so the ApiService can identify the user without requiring
/// a separate JWT authentication layer between the two services.
/// </summary>
public sealed class UserIdForwardingHandler : DelegatingHandler
{
    private const string InternalKeyHeader = "X-Internal-Key";
    private const string InternalKeyConfig = "Internal:ApiKey";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string? _internalKey;

    public UserIdForwardingHandler(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _internalKey = configuration[InternalKeyConfig];
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Signed-out visitors are not forwarded as an Account, so ApiService answers 401 instead of
        // serving data for a shared placeholder id.
        string? userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            request.Headers.Add("X-User-Id", userId);
        }

        if (!string.IsNullOrEmpty(_internalKey))
        {
            request.Headers.Add(InternalKeyHeader, _internalKey);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        return response;
    }
}
