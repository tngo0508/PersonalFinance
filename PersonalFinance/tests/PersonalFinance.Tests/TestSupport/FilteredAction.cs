using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests;

/// <summary>
/// Runs a Web controller action the way MVC does for actions marked <see cref="JsonApiErrorsAttribute"/>:
/// an exception thrown by the action becomes the filter's error result.
/// </summary>
public static partial class FilteredAction
{
    /// <summary>
    /// Invokes <paramref name="action"/>, written as <c>() =&gt; controller.Action(...)</c>, and applies the filter to
    /// what it throws. MVC runs the filter only on an action or controller marked <see cref="JsonApiErrorsAttribute"/>,
    /// so the action is checked for the attribute first: without that check, removing the attribute from production
    /// would leave every test that goes through here green (#350).
    /// </summary>
    public static async Task<IActionResult> InvokeAsync(ControllerBase controller, Func<Task<IActionResult>> action,
        [CallerArgumentExpression(nameof(action))] string actionExpression = "")
    {
        AssertMarkedForJsonApiErrors(controller.GetType(), ActionName(actionExpression));

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            var actionContext = new ActionContext(controller.HttpContext, controller.RouteData ?? new(),
                controller.ControllerContext.ActionDescriptor ?? new ActionDescriptor());
            var context = new ExceptionContext(actionContext, []) { Exception = ex };
            new JsonApiErrorsFilter(NullLogger<JsonApiErrorsFilter>.Instance, TestLocalization.Localizer).OnException(context);
            if (!context.ExceptionHandled)
            {
                throw;
            }

            return context.Result!;
        }
    }

    private static string ActionName(string actionExpression)
    {
        Match match = ActionCall().Match(actionExpression);
        Assert.True(match.Success,
            $"Pass the action as () => controller.Action(...) so its [JsonApiErrors] can be checked; got: {actionExpression}");
        return match.Groups["action"].Value;
    }

    private static void AssertMarkedForJsonApiErrors(Type controllerType, string actionName)
    {
        List<MethodInfo> actions = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == actionName)
            .ToList();
        Assert.True(actions.Count > 0, $"{controllerType.Name} has no public action {actionName}.");

        bool marked = controllerType.IsDefined(typeof(JsonApiErrorsAttribute), inherit: true)
                      || actions.Any(method => method.IsDefined(typeof(JsonApiErrorsAttribute), inherit: true));
        Assert.True(marked,
            $"{controllerType.Name}.{actionName} is not marked [JsonApiErrors]; MVC would not run the filter.");
    }

    [GeneratedRegex(@"=>\s*\w+\s*\.\s*(?<action>\w+)\s*\(")]
    private static partial Regex ActionCall();
}
