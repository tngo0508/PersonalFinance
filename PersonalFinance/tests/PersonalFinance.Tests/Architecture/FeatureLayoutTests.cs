using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Architecture;

/// <summary>
/// The Feature layout of ADR 0007: ApiService is organized by Feature, a file's namespace says where it lives (in
/// ApiService and Web), and one Feature depends on another only through its interface. Web's MVC layout (ADR 0008) is
/// checked by <see cref="WebMvcLayoutTests"/>.
/// </summary>
public class FeatureLayoutTests
{
    /// <summary>The Features (GLOSSARY.md), the single source of truth for the namespace rule.</summary>
    public static readonly IReadOnlyList<string> Features =
    [
        "NetWorth", "BudgetPlanner", "BudgetTracker", "Analysis", "GoogleDrive", "SavingsGoals", "Community",
        "Watchlist", "AiAssistant", "Feedback", "Admin", "AccountLifecycle", "Search", "Dashboard", "LandingPage", "Legal",
        "InvestmentCalculator", "Research", "MoneyPriorities"
    ];

    private static readonly Assembly ApiServiceAssembly = typeof(InfrastructureServiceExtensions).Assembly;
    private static readonly Assembly WebAssembly = typeof(InternalApiClientExtensions).Assembly;

    public static TheoryData<string> Projects => new() { "PersonalFinance.ApiService", "PersonalFinance.Web" };

    private static Assembly AssemblyOf(string project) =>
        project == "PersonalFinance.ApiService" ? ApiServiceAssembly : WebAssembly;

    [Fact]
    public void EveryApiServiceType_LivesInAFeatureOrInInfrastructure()
    {
        const string project = "PersonalFinance.ApiService";
        List<string> misplaced = SourceTypes(ApiServiceAssembly)
            .Where(t => !IsAllowedNamespace(project, t.Namespace!))
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(misplaced.Count == 0,
            $"Types outside Features.<Feature> and Infrastructure in {project} (ADR 0007):\n  " +
            string.Join("\n  ", misplaced));
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

    [Fact]
    public void ApiServiceConstructorParameters_CrossingIntoAnotherFeature_AreInterfaces()
    {
        const string project = "PersonalFinance.ApiService";
        Assembly assembly = ApiServiceAssembly;
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
        ns == $"{project}.Infrastructure" || FeatureOf(project, ns) is not null;

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
    internal static IEnumerable<Type> SourceTypes(Assembly assembly) =>
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
