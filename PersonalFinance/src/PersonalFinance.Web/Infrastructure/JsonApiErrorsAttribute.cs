using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Refit;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// For Web actions that answer JavaScript with JSON: turns an unhandled failure into <c>{ "error": "..." }</c>
/// with a fitting status code (see <see cref="JsonApiErrorsFilter"/>), so actions call their Refit client
/// without try/catch. Actions that render a view handle their own errors.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class JsonApiErrorsAttribute : TypeFilterAttribute
{
    public JsonApiErrorsAttribute() : base(typeof(JsonApiErrorsFilter))
    {
    }
}

/// <summary>
/// Maps an exception thrown by a JSON action to an error response:
/// <list type="bullet">
/// <item>ApiService error response: same status code, message from <see cref="ApiErrorMessages"/>.</item>
/// <item>ApiService unreachable: 502.</item>
/// <item>Anything else: 500 with a generic message (details are logged, never returned).</item>
/// </list>
/// A cancelled request (the browser went away) is not handled.
/// </summary>
public sealed class JsonApiErrorsFilter : IExceptionFilter
{
    public const string UnavailableMessage = "The service is temporarily unavailable. Please try again.";
    public const string UnexpectedMessage = "Something went wrong. Please try again.";

    private readonly ILogger<JsonApiErrorsFilter> _logger;

    public JsonApiErrorsFilter(ILogger<JsonApiErrorsFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is OperationCanceledException && context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        string? action = context.ActionDescriptor.DisplayName;
        int statusCode;
        string message;
        switch (context.Exception)
        {
            case ApiException apiException:
                statusCode = (int)apiException.StatusCode;
                message = ApiErrorMessages.FromApiException(apiException);
                _logger.LogWarning(apiException, "ApiService returned {StatusCode} for {Action}.", statusCode, action);
                break;
            case HttpRequestException:
            case TimeoutException:
            case TaskCanceledException:
                statusCode = StatusCodes.Status502BadGateway;
                message = UnavailableMessage;
                _logger.LogError(context.Exception, "ApiService unreachable for {Action}.", action);
                break;
            default:
                statusCode = StatusCodes.Status500InternalServerError;
                message = UnexpectedMessage;
                _logger.LogError(context.Exception, "Unexpected error in {Action}.", action);
                break;
        }

        context.Result = new ObjectResult(new { error = message }) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }
}
