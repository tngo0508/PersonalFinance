using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// Dependency-injection registrations of the Learning Feature (the Atelier, GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class LearningFeature
{
    /// <summary>
    /// The content folder: a path relative to the app's output folder, or absolute. Tests and local runs point it at a
    /// fixture folder.
    /// </summary>
    public const string ContentPathKey = "Learning:ContentPath";

    /// <summary>The content folder in the build output, copied there from the project (ADR 0019).</summary>
    public const string DefaultContentPath = "Features/Learning/Content";

    /// <summary>
    /// The Atelier content, read here so that a malformed file stops the host at startup rather than at the first
    /// request.
    /// </summary>
    public static IServiceCollection AddLearningFeature(this IServiceCollection services, IConfiguration configuration)
    {
        string path = Path.Combine(AppContext.BaseDirectory,
            configuration.GetValue<string>(ContentPathKey) ?? DefaultContentPath);
        services.AddSingleton<ILearningContent>(LearningContent.Load(path));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILearningLibraryService, LearningLibraryService>();
        return services;
    }
}
