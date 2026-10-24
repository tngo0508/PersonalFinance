using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Shared.DTOs.Research;

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

    private static async Task<CompanyLookupResultDto> LookUpFixture(string ticker)
    {
        await using ServiceProvider provider = Provider(Environments.Development, seedResearch: true);
        using IServiceScope scope = provider.CreateScope();
        CompanyLookupOutcome outcome = await scope.ServiceProvider.GetRequiredService<ICompanyProfileService>()
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

    [Fact]
    public async Task Fixture_ExampleIndexFund_IsAFund()
    {
        Assert.Equal(CompanyLookupKinds.Fund, (await LookUpFixture("EXFD")).Kind);
    }
}
