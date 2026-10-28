using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// The content folders the Learning tests read: the real one in the ApiService project, the fixture one beside these
/// tests, and throwaway copies to break.
/// </summary>
internal static class LearningContentFolders
{
    /// <summary>The real content folder (ADR 0019), read from the source tree rather than the build output.</summary>
    public static string Real =>
        Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.ApiService"), "Features", "Learning", "Content");

    /// <summary>Good fixture content: four Term Entries and two Lessons, with Vietnamese for <c>apr</c> and Lesson 2.</summary>
    public static string Fixture =>
        Path.Combine(RepositoryRoot.Find(), "PersonalFinance", "tests", "PersonalFinance.Tests", "Learning", "Fixtures",
            "Content");

    /// <summary>A new empty folder under the temp directory, deleted when disposed.</summary>
    public static TempFolder Empty() => new();

    /// <summary>A copy of <see cref="Fixture"/> to change, deleted when disposed.</summary>
    public static TempFolder CopyOfFixture()
    {
        var folder = new TempFolder();
        foreach (string file in Directory.EnumerateFiles(Fixture, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(folder.Path, System.IO.Path.GetRelativePath(Fixture, file));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return folder;
    }

    internal sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pf-learning-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
