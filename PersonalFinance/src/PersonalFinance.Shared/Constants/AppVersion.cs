namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Centralized application version metadata adhering strictly to Semantic Versioning (SemVer 2.0.0).
/// Update these values to bump the version across all projects (Shared, Data, ApiService, Web) in the solution.
/// Format: MAJOR.MINOR.PATCH[-PRERELEASE]
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// Current semantic version string (e.g. "1.0.0").
    /// </summary>
    public const string Current = "1.1.0";

    /// <summary>
    /// Application canonical name used in logging, OpenAPI titles, and UI banners.
    /// </summary>
    public const string ApplicationName = "PersonalFinance";

    // --- Semantic Version Components (SemVer 2.0.0) ---
    public const int Major = 1; // Breaking changes
    public const int Minor = 1; // New features (backwards-compatible)
    public const int Patch = 0; // Bug fixes (backwards-compatible)
    public const string? Suffix = null; // Optional: "preview.1", "alpha", "beta", "rc.1"

    /// <summary>
    /// Evaluates the full semantic version including optional pre-release tag (e.g. "1.0.0" or "1.0.0-rc.1").
    /// </summary>
    public static string FullVersion => string.IsNullOrWhiteSpace(Suffix)
        ? $"{Major}.{Minor}.{Patch}"
        : $"{Major}.{Minor}.{Patch}-{Suffix}";

    /// <summary>
    /// Formatted application title and version banner.
    /// </summary>
    public static string InformationalVersion => $"{ApplicationName} v{FullVersion}";
}