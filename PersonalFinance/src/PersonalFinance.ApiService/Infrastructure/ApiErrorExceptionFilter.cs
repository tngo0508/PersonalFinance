using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     Global MVC filter that turns an <see cref="ApiErrorException"/> thrown by a controller or service into its
///     status code and <see cref="PersonalFinance.Shared.Contracts.ApiErrorDto"/> body (ADR 0017). Any other exception
///     is left to the normal pipeline.
/// </summary>
public sealed class ApiErrorExceptionFilter(ILogger<ApiErrorExceptionFilter> logger) : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiErrorException error)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Request rejected with {ErrorKey} ({StatusCode})", error.Key, error.StatusCode);
        }

        context.Result = new ObjectResult(error.ToDto()) { StatusCode = error.StatusCode };
        context.ExceptionHandled = true;
    }
}
