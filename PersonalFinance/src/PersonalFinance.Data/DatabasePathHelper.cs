using Microsoft.Data.Sqlite;

namespace PersonalFinance.Data;

/// <summary>
/// Helper to resolve consistent SQLite database paths across all projects (API, Web, EF CLI)
/// ensuring a single shared database at the solution root during local development.
/// </summary>
public static class DatabasePathHelper
{
    public const string DefaultDatabaseFileName = "PersonalFinance.db";

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

    /// <summary>
    /// Gets the absolute file path to the SQLite database located at the solution root.
    /// </summary>
    public static string GetDatabasePath(string databaseFileName = DefaultDatabaseFileName)
    {
        return Path.Combine(GetSolutionRoot(), databaseFileName);
    }

    /// <summary>
    /// Constructs a SQLite connection string pointing to the shared solution root database.
    /// </summary>
    public static string GetSqliteConnectionString(string databaseFileName = DefaultDatabaseFileName)
    {
        return $"Data Source={GetDatabasePath(databaseFileName)}";
    }

    /// <summary>
    /// Resolves relative SQLite connection strings to point to the shared solution root.
    /// Absolute paths, in-memory configurations, or non-SQLite strings are left intact.
    /// </summary>
    public static string ResolveConnectionString(string? connectionString, string defaultDatabaseName = DefaultDatabaseFileName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return GetSqliteConnectionString(defaultDatabaseName);
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;

            if (string.IsNullOrWhiteSpace(dataSource))
            {
                builder.DataSource = GetDatabasePath(defaultDatabaseName);
                return builder.ConnectionString;
            }

            // If in-memory or already absolute, preserve as-is
            if (dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase) ||
                dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                Path.IsPathRooted(dataSource))
            {
                return connectionString;
            }

            builder.DataSource = GetDatabasePath(dataSource);
            return builder.ConnectionString;
        }
        catch
        {
            return connectionString;
        }
    }
}
