using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// How the Companies tab is registered: against SEC EDGAR normally, and against the made-up
/// <see cref="FixtureSecCompanyClient"/> only in Development with <c>E2E_SEED_RESEARCH=true</c>, where the E2E
/// walkthrough looks up its three tickers.
/// </summary>
public class CompanyProfileFixtureTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "PersonalFinance.ApiService";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider Provider(string environment, bool seedResearch)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ResearchFixtureSeeder.Setting] = seedResearch ? "true" : "false",
            })
            .Build();
        return new ServiceCollection()
            .AddLogging()
            .AddResearchFeature(configuration, new Environment(environment))
            .BuildServiceProvider();
    }

    // The fixture dates Example Corp's recent filings back from today; on this day its latest is 4 August 2026.
    private static readonly DateTimeOffset FixtureNow = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static async Task<CompanyLookupResultDto> LookUpFixture(string ticker)
    {
        await using ServiceProvider provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(FixtureNow))
            .AddResearchFeature(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [ResearchFixtureSeeder.Setting] = "true" })
                .Build(), new Environment(Environments.Development))
            .BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        CompanyLookupOutcome<CompanyLookupResultDto> outcome = await scope.ServiceProvider.GetRequiredService<ICompanyProfileService>()
            .LookUpAsync("account-1", ticker);
        return Assert.IsType<CompanyLookupResultDto>(outcome.Result);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public void OutsideE2EFixtureMode_CompaniesComeFromSec(string environment, bool seedResearch)
    {
        using ServiceProvider provider = Provider(environment, seedResearch);
        using IServiceScope scope = provider.CreateScope();

        Assert.IsType<SecEdgarClient>(scope.ServiceProvider.GetRequiredService<ISecCompanyClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICompanyProfileService>());
    }

    [Fact]
    public void InE2EFixtureMode_CompaniesComeFromTheFixture()
    {
        using ServiceProvider provider = Provider(Environments.Development, seedResearch: true);
        using IServiceScope scope = provider.CreateScope();

        Assert.IsType<FixtureSecCompanyClient>(scope.ServiceProvider.GetRequiredService<ISecCompanyClient>());
    }

    [Fact]
    public async Task Fixture_ExampleCorp_HasFourYears_WithTheCasesThePageShows()
    {
        CompanyLookupResultDto result = await LookUpFixture("EXMP");

        CompanyProfileDto profile = Assert.IsType<CompanyProfileDto>(result.Profile);
        Assert.Equal("Example Corp", profile.Name);
        Assert.Equal([2022, 2023, 2024, 2025], profile.FiscalYears.Select(y => y.FiscalYear));
        Assert.Null(profile.FiscalYears[0].GrossProfit); // A "Not reported" cell.
        Assert.Equal(1_200_000_000m, profile.FiscalYears[0].Revenue); // Read from SalesRevenueNet.
        Assert.Equal(-35_000_000m, profile.FiscalYears[2].NetIncome); // The restated loss.
        Assert.Contains(profile.Filings, f => f.Events.Contains("Results of operations"));
        Assert.DoesNotContain(profile.Filings, f => f.Form == "4");
        Assert.Equal(new DateOnly(2026, 8, 4), profile.DataAsOf);
    }

    [Fact]
    public async Task Fixture_ExampleGlobal_ReportsUnderIfrs()
    {
        CompanyProfileDto profile = Assert.IsType<CompanyProfileDto>((await LookUpFixture("EXGL")).Profile);

        Assert.True(profile.ReportsUnderIfrs);
        Assert.Empty(profile.FiscalYears);
        Assert.Equal(["6-K", "20-F"], profile.Filings.Select(f => f.Form));
    }

    private static async Task<CompanyPressReleasesResultDto> PressReleasesOfFixture(string ticker,
        DateTimeOffset? now = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ResearchFixtureSeeder.Setting] = "true" })
            .Build();
        await using ServiceProvider provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(now ?? FixtureNow))
            .AddResearchFeature(configuration, new Environment(Environments.Development))
            .BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        CompanyLookupOutcome<CompanyPressReleasesResultDto> outcome = await scope.ServiceProvider.GetRequiredService<ICompanyProfileService>()
            .GetPressReleasesAsync("account-1", ticker);
        return Assert.IsType<CompanyPressReleasesResultDto>(outcome.Result);
    }

    [Fact]
    public async Task Fixture_ExampleCorp_HasThreePressReleases_OneWithAFallbackHeadline()
    {
        CompanyPressReleasesResultDto result = await PressReleasesOfFixture("EXMP");

        Assert.Equal(CompanyLookupKinds.Profile, result.Kind);
        Assert.Equal(
        [
            "Example Corp Reports Second Quarter 2026 Results",
            "Example Corp Names Alex Rivera Chief Financial Officer",
            "Results of operations · Regulation FD disclosure · Financial statements and exhibits",
        ], result.Releases.Select(r => r.Headline));
        Assert.Equal([true, true, false], result.Releases.Select(r => r.HeadlineRead));
        Assert.All(result.Releases,
            r => Assert.StartsWith("https://www.sec.gov/Archives/edgar/data/9000001/", r.Url, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2026, 10, 24)]
    [InlineData(2027, 6, 1)]
    [InlineData(2031, 2, 28)]
    public async Task Fixture_ExampleCorp_KeepsItsThreePressReleases_WhateverTheDate(int year, int month, int day)
    {
        // The E2E walkthrough runs on whatever day CI does, so the fixture's 8-Ks stay within the last six months.
        var today = new DateOnly(year, month, day);

        CompanyPressReleasesResultDto result = await PressReleasesOfFixture("EXMP",
            new DateTimeOffset(today, new TimeOnly(12, 0), TimeSpan.Zero));

        Assert.Equal(3, result.Releases.Count);
        Assert.All(result.Releases, r => Assert.InRange(r.FiledOn, today.AddMonths(-6), today));
    }

    [Fact]
    public async Task Fixture_ExampleGlobal_HasNoPressReleases()
    {
        CompanyPressReleasesResultDto result = await PressReleasesOfFixture("EXGL");

        Assert.Equal(CompanyLookupKinds.Profile, result.Kind);
        Assert.Empty(result.Releases);
    }

    [Fact]
    public async Task Fixture_ExampleIndexFund_IsAFund()
    {
        Assert.Equal(CompanyLookupKinds.Fund, (await LookUpFixture("EXFD")).Kind);
    }
}
