using System.Text.Json;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// ADR 0014: Mermaid is pinned exactly to 11.17.2, vendored through libman from jsDelivr, licensed MIT, and loaded only
/// by the Money Priorities page, never from the shared layout.
/// </summary>
public class MermaidVendoringTests
{
    private static readonly string WebProject = RepositoryRoot.SourceProject("PersonalFinance.Web");

    private static JsonElement MermaidLibmanEntry()
    {
        using JsonDocument libman = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebProject, "libman.json")));
        List<JsonElement> entries = libman.RootElement.GetProperty("libraries").EnumerateArray()
            .Where(entry => entry.GetProperty("library").GetString()!.StartsWith("mermaid@", StringComparison.Ordinal))
            .Select(entry => entry.Clone())
            .ToList();
        return Assert.Single(entries);
    }

    [Fact]
    public void Libman_PinsMermaid11_17_2_FromJsDelivr_IntoLibMermaid()
    {
        JsonElement entry = MermaidLibmanEntry();

        Assert.Equal("mermaid@11.17.2", entry.GetProperty("library").GetString());
        Assert.Equal("jsdelivr", entry.GetProperty("provider").GetString());
        Assert.Equal("wwwroot/lib/mermaid", entry.GetProperty("destination").GetString());
        string[] files = entry.GetProperty("files").EnumerateArray().Select(file => file.GetString()!).ToArray();
        Assert.Equal(["dist/mermaid.min.js", "LICENSE"], files);
    }

    [Fact]
    public void VendoredMermaid_IsCommitted_WithItsMitLicence()
    {
        string library = Path.Combine(WebProject, "wwwroot", "lib", "mermaid");

        Assert.True(File.Exists(Path.Combine(library, "dist", "mermaid.min.js")), "wwwroot/lib/mermaid/dist/mermaid.min.js is missing");
        string licence = File.ReadAllText(Path.Combine(library, "LICENSE"));
        Assert.StartsWith("The MIT License", licence, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheMoneyPrioritiesViews_ReferenceMermaid()
    {
        string moneyPrioritiesViews = Path.Combine(WebProject, "Views", "MoneyPriorities") + Path.DirectorySeparatorChar;

        List<string> offenders = Directory.EnumerateFiles(WebProject, "*.cshtml", SearchOption.AllDirectories)
            .Where(view => !view.StartsWith(moneyPrioritiesViews, StringComparison.OrdinalIgnoreCase))
            .Where(view => File.ReadAllText(view).Contains("lib/mermaid", StringComparison.OrdinalIgnoreCase))
            .Select(view => Path.GetRelativePath(WebProject, view))
            .ToList();

        Assert.Empty(offenders);
    }
}
