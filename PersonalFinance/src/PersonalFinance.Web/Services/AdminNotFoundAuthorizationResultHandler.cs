using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Web.Services;

/// <summary>
/// Returns 404 when a signed-in account fails the <see cref="AppPolicies.RequireAdmin"/> policy, so the admin area stays hidden.
/// Every other outcome (login redirect, other policies) is delegated to the default handler.
/// </summary>
public class AdminNotFoundAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && RequiresAdminPolicy(context))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }

    private static bool RequiresAdminPolicy(HttpContext context)
    {
        IReadOnlyList<IAuthorizeData>? authorizeData = context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>();
        return authorizeData?.Any(a => a.Policy == AppPolicies.RequireAdmin) == true;
    }
}
