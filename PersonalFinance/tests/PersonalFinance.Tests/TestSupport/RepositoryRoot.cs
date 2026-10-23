namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// Locates the repository root (the directory holding <c>PersonalFinance.sln</c>) from the test output directory,
/// for tests that read source files or workflow definitions.
/// </summary>
internal static class RepositoryRoot
{
    public static string Find()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PersonalFinance.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not locate the repository root (no PersonalFinance.sln found).");
    }

    public static string SourceProject(string projectName) =>
        Path.Combine(Find(), "PersonalFinance", "src", projectName);
}
