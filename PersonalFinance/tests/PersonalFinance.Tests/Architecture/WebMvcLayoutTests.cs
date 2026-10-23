using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Architecture;

/// <summary>
/// The classic MVC layout of ADR 0008: Web's controllers live in <c>Controllers/</c>, view models in
/// <c>Models/&lt;Controller&gt;/</c> beside <c>Views/&lt;Controller&gt;/</c>, so a page's parts are found from one another
/// by name.
/// </summary>
public class WebMvcLayoutTests
{
    private const string Project = "PersonalFinance.Web";
    private static readonly Assembly WebAssembly = typeof(InternalApiClientExtensions).Assembly;

    /// <summary>Namespaces a Web type may live in besides <c>Models.&lt;Controller&gt;</c> and the Identity area.</summary>
    private static readonly IReadOnlyList<string> FixedNamespaces =
        ["Controllers", "Services", "TagHelpers", "Infrastructure"];

    [Fact]
    public void EveryWebType_LivesInAnMvcFolder()
    {
        HashSet<string> controllerNames = ControllerNames();
        List<string> misplaced = FeatureLayoutTests.SourceTypes(WebAssembly)
            .Where(t => !IsAllowedNamespace(t.Namespace!, controllerNames))
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(misplaced.Count == 0,
            "Web types outside Controllers, Models.<Controller>, Services, TagHelpers, Infrastructure and the Identity " +
            "area (ADR 0008):\n  " + string.Join("\n  ", misplaced));
    }

    [Fact]
    public void EveryController_LivesInControllers()
    {
        List<string> misplaced = Controllers()
            .Where(t => t.Namespace != $"{Project}.Controllers")
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(misplaced.Count == 0,
            "Web controllers outside Controllers (ADR 0008):\n  " + string.Join("\n  ", misplaced));
    }

    [Fact]
    public void EveryViewFolder_HasAController()
    {
        HashSet<string> controllerNames = ControllerNames();
        string viewsDirectory = Path.Combine(RepositoryRoot.SourceProject(Project), "Views");
        List<string> orphans = Directory.EnumerateDirectories(viewsDirectory)
            .Select(Path.GetFileName)
            .Where(name => name != "Shared" && !controllerNames.Contains(name!))
            .Select(name => $"Views/{name}")
            .Order(StringComparer.Ordinal)
            .ToList()!;

        Assert.True(orphans.Count == 0,
            "View folders with no controller of the same name (ADR 0008):\n  " + string.Join("\n  ", orphans));
    }

    private static bool IsAllowedNamespace(string ns, HashSet<string> controllerNames)
    {
        if (ns.StartsWith($"{Project}.Areas.", StringComparison.Ordinal)
            || FixedNamespaces.Any(folder => ns == $"{Project}.{folder}"))
        {
            return true;
        }

        string modelsPrefix = $"{Project}.Models.";
        return ns.StartsWith(modelsPrefix, StringComparison.Ordinal)
            && controllerNames.Contains(ns[modelsPrefix.Length..]);
    }

    private static IEnumerable<Type> Controllers() =>
        FeatureLayoutTests.SourceTypes(WebAssembly)
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    /// <summary>Controller names as MVC routes and view folders use them: <c>BudgetController</c> is <c>Budget</c>.</summary>
    private static HashSet<string> ControllerNames() =>
        Controllers()
            .Select(t => t.Name[..^"Controller".Length])
            .ToHashSet(StringComparer.Ordinal);
}
