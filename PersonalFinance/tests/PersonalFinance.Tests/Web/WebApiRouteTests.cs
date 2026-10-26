using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// Browser JavaScript calls Web, never ApiService directly: every <c>/api/X</c> path in wwwroot/js needs a
/// signed-in-only Web controller routed at <c>api/X</c>.
/// </summary>
public partial class WebApiRouteTests
{
    [Fact]
    public void EveryApiPathCalledFromJavaScript_HasAnAuthorizedWebController()
    {
        string jsRoot = Path.Combine(FindRepositoryRoot(), "PersonalFinance", "src", "PersonalFinance.Web", "wwwroot", "js");
        var calledControllers = Directory.EnumerateFiles(jsRoot, "*.js", SearchOption.AllDirectories)
            .SelectMany(file => ApiPathPattern().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var apiControllers = typeof(HomeController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
                .Cast<RouteAttribute>()
                .Any(r => r.Template.StartsWith("api/", StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(t => t.Name[..^"Controller".Length], StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(calledControllers);
        var missing = calledControllers.Where(name => !apiControllers.ContainsKey(name)).ToList();
        Assert.True(missing.Count == 0, $"JavaScript calls /api/ paths with no Web controller: {string.Join(", ", missing)}");
        Assert.All(apiControllers.Values, t => Assert.True(t.IsDefined(typeof(AuthorizeAttribute), inherit: true),
            $"{t.Name} serves /api/ for JavaScript but has no [Authorize]."));
    }

    [Fact]
    public void JsonApiErrorsFilter_LeavesARequestTheBrowserCancelledUnhandled()
    {
        var httpContext = new DefaultHttpContext();
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        httpContext.RequestAborted = aborted.Token;
        var context = new ExceptionContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), [])
        {
            Exception = new OperationCanceledException()
        };

        new JsonApiErrorsFilter(NullLogger<JsonApiErrorsFilter>.Instance, TestLocalization.Localizer).OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [GeneratedRegex(@"['""`]/api/([A-Za-z]+)")]
    private static partial Regex ApiPathPattern();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PersonalFinance.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
