namespace PersonalFinance.Data;

/// <summary>
/// Helper to resolve the solution root directory across all projects (API, Web, EF CLI).
/// </summary>
public static class DatabasePathHelper
{
    /// <summary>
    /// Finds the solution root directory by searching upwards for a .sln file, falling back to Directory.Packages.props or current directory.
    /// </summary>
    public static string GetSolutionRoot()
    {
        var candidateDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        // First pass: look specifically for .sln file
        foreach (var startDir in candidateDirectories)
        {
            if (string.IsNullOrWhiteSpace(startDir) || !Directory.Exists(startDir))
            {
                continue;
            }

            var directory = new DirectoryInfo(startDir);
            while (directory != null)
            {
                if (directory.GetFiles("*.sln").Length > 0)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        // Second pass: look for Directory.Packages.props or Directory.Build.props
        foreach (var startDir in candidateDirectories)
        {
            if (string.IsNullOrWhiteSpace(startDir) || !Directory.Exists(startDir))
            {
                continue;
            }

            var directory = new DirectoryInfo(startDir);
            while (directory != null)
            {
                if (directory.GetFiles("Directory.Packages.props").Length > 0 ||
                    directory.GetFiles("Directory.Build.props").Length > 0)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
