using System.Text.Json;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// ADR 0020: Cytoscape.js and its dagre extension are pinned exactly, vendored through libman from jsDelivr with their
/// MIT licences committed, and loaded only by the Financial Universe view, never from the shared layout.
/// </summary>
public class CytoscapeVendoringTests
{
    private static readonly string WebProject = RepositoryRoot.SourceProject("PersonalFinance.Web");

    private static JsonElement LibmanEntry(string prefix)
    {
        using JsonDocument libman = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebProject, "libman.json")));
        List<JsonElement> entries = libman.RootElement.GetProperty("libraries").EnumerateArray()
            .Where(entry => entry.GetProperty("library").GetString()!.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => entry.Clone())
            .ToList();
        return Assert.Single(entries);
    }

    [Theory]
    [InlineData("cytoscape@", "cytoscape@3.34.4", "wwwroot/lib/cytoscape", "dist/cytoscape.min.js")]
    [InlineData("cytoscape-dagre@", "cytoscape-dagre@4.0.1", "wwwroot/lib/cytoscape-dagre", "dist/cytoscape-dagre.min.js")]
    public void Libman_PinsThePackage_FromJsDelivr_WithItsLicence(string prefix, string library, string destination, string script)
    {
        JsonElement entry = LibmanEntry(prefix);

        Assert.Equal(library, entry.GetProperty("library").GetString());
        Assert.Equal("jsdelivr", entry.GetProperty("provider").GetString());
        Assert.Equal(destination, entry.GetProperty("destination").GetString());
        string[] files = entry.GetProperty("files").EnumerateArray().Select(file => file.GetString()!).ToArray();
        Assert.Equal([script, "LICENSE"], files);
    }

    [Theory]
    [InlineData("cytoscape", "cytoscape.min.js")]
    [InlineData("cytoscape-dagre", "cytoscape-dagre.min.js")]
    public void VendoredPackage_IsCommitted_WithItsMitLicence(string folder, string script)
    {
        string library = Path.Combine(WebProject, "wwwroot", "lib", folder);

        Assert.True(File.Exists(Path.Combine(library, "dist", script)), $"wwwroot/lib/{folder}/dist/{script} is missing");
        string licence = File.ReadAllText(Path.Combine(library, "LICENSE"));
        Assert.Contains("Permission is hereby granted, free of charge", licence, StringComparison.Ordinal);
        Assert.Contains("The Cytoscape Consortium", licence, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheFinancialUniverseViews_ReferenceCytoscape()
    {
        string financialUniverseViews = Path.Combine(WebProject, "Views", "FinancialUniverse") + Path.DirectorySeparatorChar;

        List<string> offenders = Directory.EnumerateFiles(WebProject, "*.cshtml", SearchOption.AllDirectories)
            .Where(view => !view.StartsWith(financialUniverseViews, StringComparison.OrdinalIgnoreCase))
            .Where(view => File.ReadAllText(view).Contains("lib/cytoscape", StringComparison.OrdinalIgnoreCase))
            .Select(view => Path.GetRelativePath(WebProject, view))
            .ToList();

        Assert.Empty(offenders);
    }
}
