using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests;

/// <summary>
/// Runs a Web controller action the way MVC does for actions marked <see cref="JsonApiErrorsAttribute"/>:
/// an exception thrown by the action becomes the filter's error result.
/// </summary>
public static class FilteredAction
{
    public static async Task<IActionResult> InvokeAsync(ControllerBase controller, Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            var actionContext = new ActionContext(controller.HttpContext, controller.RouteData ?? new(),
                controller.ControllerContext.ActionDescriptor ?? new ActionDescriptor());
            var context = new ExceptionContext(actionContext, []) { Exception = ex };
            new JsonApiErrorsAttribute().OnException(context);
            if (!context.ExceptionHandled)
            {
                throw;
            }

            return context.Result!;
        }
    }
}
