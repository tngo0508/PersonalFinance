using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Architecture;

/// <summary>
/// The Feature layout of ADR 0007: ApiService and Web are organized by Feature, a file's namespace says where it
/// lives, and one Feature depends on another only through its interface. The type-based namespaces that ADR 0001 §3
/// chose are tolerated through <see cref="LegacyNamespaces"/> while the move is in progress.
/// </summary>
public class FeatureLayoutTests
{
    /// <summary>The Features (GLOSSARY.md), the single source of truth for the namespace rule.</summary>
    public static readonly IReadOnlyList<string> Features =
    [
        "NetWorth", "BudgetPlanner", "BudgetTracker", "Analysis", "GoogleDrive", "SavingsGoals", "Community",
        "Watchlist", "AiAssistant", "Feedback", "Admin", "AccountLifecycle", "Search", "Dashboard"
    ];

    /// <summary>
    /// Namespaces from the type-based layout that ADR 0007 replaces. Each migrate ticket (#157 to #160) deletes the
    /// entries it empties, and #161 deletes the list. Nothing is ever added here.
    /// </summary>
    private static readonly HashSet<string> LegacyNamespaces = new(StringComparer.Ordinal)
    {
        "PersonalFinance.ApiService.Controllers",
        "PersonalFinance.ApiService.Services",
        "PersonalFinance.ApiService.Validators",
        "PersonalFinance.Web.Controllers",
        "PersonalFinance.Web.Models",
        "PersonalFinance.Web.Services",
        "PersonalFinance.Web.TagHelpers"
    };

    private static readonly Assembly ApiServiceAssembly = typeof(FeatureServiceExtensions).Assembly;
    private static readonly Assembly WebAssembly = typeof(InternalApiClientExtensions).Assembly;

    public static TheoryData<string> Projects => new() { "PersonalFinance.ApiService", "PersonalFinance.Web" };

    private static Assembly AssemblyOf(string project) =>
        project == "PersonalFinance.ApiService" ? ApiServiceAssembly : WebAssembly;

    [Theory]
    [MemberData(nameof(Projects))]
    public void EveryType_LivesInAFeatureOrInInfrastructure(string project)
    {
        List<string> misplaced = SourceTypes(AssemblyOf(project))
            .Where(t => !IsAllowedNamespace(project, t.Namespace!))
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(misplaced.Count == 0,
            $"Types outside Features.<Feature> and Infrastructure in {project} (ADR 0007):\n  " +
            string.Join("\n  ", misplaced));
    }

    [Fact]
    public void LegacyAllowList_NamesOnlyNamespacesStillInUse()
    {
        // A migrate ticket that empties a legacy namespace must also delete its entry, so the list only shrinks.
        HashSet<string> inUse = SourceTypes(ApiServiceAssembly).Concat(SourceTypes(WebAssembly))
            .Select(t => t.Namespace!)
            .ToHashSet(StringComparer.Ordinal);

        List<string> stale = LegacyNamespaces.Where(ns => !inUse.Contains(ns)).Order(StringComparer.Ordinal).ToList();

        Assert.True(stale.Count == 0,
            "Legacy namespaces with no types left; remove them from the allow-list:\n  " + string.Join("\n  ", stale));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void EveryFile_DeclaresTheNamespaceItsFolderImplies(string project)
    {
        string projectDirectory = RepositoryRoot.SourceProject(project);
        var mismatches = new List<string>();

        foreach (string file in SourceFiles(projectDirectory))
        {
            Match declaration = Regex.Match(File.ReadAllText(file), @"^namespace\s+([\w.]+)\s*[;{]", RegexOptions.Multiline);
            if (!declaration.Success)
            {
                continue; // Program.cs: top-level statements, no namespace.
            }

            string relativeFolder = Path.GetRelativePath(projectDirectory, Path.GetDirectoryName(file)!);
            string expected = relativeFolder == "."
                ? project
                : $"{project}.{relativeFolder.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.')}";

            if (declaration.Groups[1].Value != expected)
            {
                mismatches.Add($"{Path.GetRelativePath(projectDirectory, file)}: {declaration.Groups[1].Value} (expected {expected})");
            }
        }

        Assert.True(mismatches.Count == 0,
            $"Files whose namespace does not match their folder in {project}:\n  " + string.Join("\n  ", mismatches));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void ConstructorParameters_CrossingIntoAnotherFeature_AreInterfaces(string project)
    {
        Assembly assembly = AssemblyOf(project);
        var violations = new List<string>();

        foreach (Type type in SourceTypes(assembly).Where(t => !t.IsInterface))
        {
            string? feature = FeatureOf(project, type.Namespace!);
            if (feature is null)
            {
                continue;
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    foreach (Type dependency in Unwrap(parameter.ParameterType))
                    {
                        if (dependency.Assembly != assembly || dependency.IsInterface)
                        {
                            continue;
                        }

                        string? dependencyFeature = FeatureOf(project, dependency.Namespace ?? string.Empty);
                        if (dependencyFeature is not null && dependencyFeature != feature)
                        {
                            violations.Add($"{type.FullName}({parameter.Name}: {dependency.Name}) reaches into {dependencyFeature}");
                        }
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"A Feature may depend on another Feature only through its interface (ADR 0007), in {project}:\n  " +
            string.Join("\n  ", violations));
    }

    private static bool IsAllowedNamespace(string project, string ns) =>
        ns == $"{project}.Infrastructure"
        || FeatureOf(project, ns) is not null
        || (project == "PersonalFinance.Web" && ns.StartsWith("PersonalFinance.Web.Areas.", StringComparison.Ordinal))
        || LegacyNamespaces.Contains(ns);

    private static string? FeatureOf(string project, string ns)
    {
        string prefix = $"{project}.Features.";
        if (!ns.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string feature = ns[prefix.Length..].Split('.')[0];
        return Features.Contains(feature) ? feature : null;
    }

    /// <summary>Hand-written, top-level types; compiler, Razor and embedded-attribute output is left out.</summary>
    private static IEnumerable<Type> SourceTypes(Assembly assembly) =>
        assembly.GetTypes().Where(t =>
            !t.IsNested
            && t.Namespace is not null
            && !t.Name.StartsWith('<')
            && t.GetCustomAttribute<CompilerGeneratedAttribute>() is null
            && !t.Namespace.StartsWith("AspNetCoreGeneratedDocument", StringComparison.Ordinal)
            && !t.Namespace.StartsWith("System.", StringComparison.Ordinal)
            && !t.Namespace.StartsWith("Microsoft.", StringComparison.Ordinal));

    private static IEnumerable<string> SourceFiles(string projectDirectory) =>
        Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                string relative = Path.GetRelativePath(projectDirectory, f);
                return !relative.StartsWith("obj", StringComparison.Ordinal)
                    && !relative.StartsWith("bin", StringComparison.Ordinal)
                    && !relative.StartsWith("wwwroot", StringComparison.Ordinal);
            });

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsByRef || type.IsArray)
        {
            type = type.GetElementType()!;
        }

        yield return type;
        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments().SelectMany(Unwrap))
            {
                yield return argument;
            }
        }
    }
}
