using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Feature's configuration: the Public Float exclusion list is read from <c>Research:PublicFloatExclusions</c>,
/// and the list shipped in the ApiService's <c>appsettings.json</c> holds the filer years reviewed against SEC's frames.
/// </summary>
public class ResearchFeatureTests
{
    private static ResearchSettings SettingsFrom(IConfiguration configuration) =>
        new ServiceCollection()
            .AddResearchFeature(configuration)
            .BuildServiceProvider()
            .GetRequiredService<ResearchSettings>();

    [Fact]
    public void AddResearchFeature_ReadsThePublicFloatExclusionsFromConfiguration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Research:PublicFloatExclusions:0:Cik"] = "854560",
                ["Research:PublicFloatExclusions:0:Year"] = "2017",
                ["Research:PublicFloatExclusions:0:Filer"] = "GREAT SOUTHERN BANCORP, INC.",
                ["Research:PublicFloatExclusions:1:Cik"] = "1853513",
                ["Research:PublicFloatExclusions:1:Year"] = "2025",
            })
            .Build();

        ResearchSettings settings = SettingsFrom(configuration);

        Assert.Equal([new FilerYear(854560, 2017), new FilerYear(1853513, 2025)], settings.PublicFloatExclusions);
    }

    [Fact]
    public void AddResearchFeature_WithNoExclusionsConfigured_ExcludesNothing()
    {
        ResearchSettings settings = SettingsFrom(new ConfigurationBuilder().Build());

        Assert.Empty(settings.PublicFloatExclusions);
    }

    [Fact]
    public void ShippedExclusionList_HoldsThePrototypesReviewedRows_OnceEach()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.ApiService"), "appsettings.json"))
            .Build();

        IReadOnlyList<FilerYear> exclusions = SettingsFrom(configuration).PublicFloatExclusions;

        // The rows the prototype excluded by hand, confirmed by CIK and year in SEC's frames; WGL Holdings
        // (CIK 1103601) needs no row, as the 200x-total-assets rule drops its 2012-2014 values.
        Assert.Subset(exclusions.ToHashSet(), new HashSet<FilerYear>
        {
            new(1209821, 2011), new(1209821, 2012), new(1179060, 2012),
            new(854560, 2017), new(854560, 2018), new(854560, 2019), new(854560, 2020),
            new(1527590, 2022), new(1527590, 2023),
            new(1853513, 2025),
        });
        Assert.DoesNotContain(exclusions, e => e.Cik == 1103601);
        Assert.Equal(exclusions.Count, exclusions.Distinct().Count());
        Assert.All(exclusions, e => Assert.InRange(e.Year, 2011, 2100));
    }
}
