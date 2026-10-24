using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PersonalFinance.ApiService.Features.Admin;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The E2E Research fixtures: small Sectors, funds, fund overlap and World snapshots the ApiService stores at startup when
/// <c>E2E_SEED_RESEARCH=true</c> in Development, so the Playwright walkthrough runs with no network to SEC or the
/// World Bank. Never in Production.
/// </summary>
[Collection("ApiServiceHost")]
public class ResearchFixtureSeederTests
{
    private const string InternalKey = "fixture-test-shared-secret";

    [Theory]
    [InlineData("Development", "true", true)]
    [InlineData("Development", "TRUE", true)]
    [InlineData("Development", null, false)]
    [InlineData("Development", "false", false)]
    [InlineData("Production", "true", false)]
    [InlineData("Staging", "true", false)]
    [InlineData("Testing", "true", false)]
    public void IsEnabled_OnlyInDevelopmentWithTheSettingOn(string environment, string? setting, bool expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ResearchFixtureSeeder.Setting] = setting })
            .Build();

        Assert.Equal(expected, ResearchFixtureSeeder.IsEnabled(new Environment(environment), configuration));
    }

    [Fact]
    public async Task Development_WithTheSettingOn_ServesEveryFixtureSnapshot()
    {
        await using var host = new ApiServiceHost(Environments.Development, seed: true);
        HttpClient client = host.CreateClient();

        SectorsSnapshotDto? sectors = await GetAsync<SectorsSnapshotDto>(client, "sectors");
        FundsSnapshotDto? funds = await GetAsync<FundsSnapshotDto>(client, "funds");
        WorldSnapshotDto? world = await GetAsync<WorldSnapshotDto>(client, "world");
        FundOverlapSnapshotDto? overlap = await GetAsync<FundOverlapSnapshotDto>(client, "funds/overlap");
        FundReturnsSnapshotDto? returns = await GetAsync<FundReturnsSnapshotDto>(client, "funds/returns");

        // What the walkthrough needs: several years to scrub, a Technology highlight, SPY and QQQ by quarter, and a
        // % of GDP figure for the World switch.
        Assert.NotNull(sectors);
        Assert.True(sectors.Periods.Count >= 2);
        Assert.Contains(sectors.Industries, i => i.Technology);
        Assert.Equal(sectors.Periods, sectors.Values.Select(v => v.Period));
        Assert.All(sectors.Values.SelectMany(v => v.Ranked), r => Assert.InRange(r.Company, 0, sectors.Companies.Count - 1));

        Assert.NotNull(funds);
        Assert.True(funds.Periods.Count >= 2);
        Assert.Contains(" Q", funds.Periods[^1]);
        Assert.Equal(["SPY", "QQQ"], funds.Funds.Select(f => f.Ticker));
        Assert.Contains(funds.Values[^1].Funds.Single(f => f.Ticker == "SPY").Categories, c => c.Code == "EC");

        Assert.NotNull(overlap);
        Assert.Equal(["SPY", "QQQ"], overlap.Funds.Select(f => f.Ticker));
        FundOverlapPairDto pair = Assert.Single(overlap.Pairs);
        Assert.All(pair.Shared, h => Assert.InRange(h.Holding, 0, overlap.Holdings.Count - 1));

        Assert.NotNull(returns);
        Assert.Equal(["SPY", "QQQ"], returns.Funds.Select(f => f.Ticker));
        Assert.All(returns.Funds, f => Assert.Equal(60, f.Returns.Count));
        Assert.All(returns.Windows, w => Assert.Single(w.Correlations));
        Assert.All(returns.Windows, w => Assert.All(w.Funds, f => Assert.NotNull(f.Sharpe)));

        Assert.NotNull(world);
        Assert.True(world.Periods.Count >= 2);
        Assert.Contains(world.Values[^1].Countries, c => c.PercentOfGdp is not null);
        Assert.Contains(world.Countries, c => c.Id == "840");
    }

    [Fact]
    public async Task Production_WithTheSettingOn_SeedsNothing()
    {
        await using var host = new ApiServiceHost(Environments.Production, seed: true);
        HttpClient client = host.CreateClient();

        foreach (string dataset in new[] { "sectors", "funds", "world" })
        {
            HttpResponseMessage response = await client.SendAsync(Request(dataset));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
    }

    [Fact]
    public async Task SeedAsync_KeepsASnapshotAlreadyStored()
    {
        await using AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ResearchFixtures_{Guid.NewGuid():N}").Options);
        db.ResearchSnapshots.Add(new ResearchSnapshot
        {
            Dataset = ResearchDatasets.World, FirstPeriod = "1990", LastPeriod = "2025", Payload = "{}",
            SourceAsOf = new DateOnly(2026, 7, 1),
        });
        await db.SaveChangesAsync();

        await ResearchFixtureSeeder.SeedAsync(db, TimeProvider.System);

        List<ResearchSnapshot> rows = await db.ResearchSnapshots.AsNoTracking().OrderBy(s => s.Dataset).ToListAsync();
        Assert.Equal([ResearchDatasets.Funds, ResearchDatasets.FundOverlap, ResearchDatasets.FundReturns, ResearchDatasets.Sectors,
                ResearchDatasets.World],
            rows.Select(r => r.Dataset));
        Assert.Equal("{}", rows.Single(r => r.Dataset == ResearchDatasets.World).Payload);
    }

    private static async Task<T?> GetAsync<T>(HttpClient client, string dataset)
    {
        HttpResponseMessage response = await client.SendAsync(Request(dataset));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private static HttpRequestMessage Request(string dataset)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Research/{dataset}");
        request.Headers.Add("X-User-Id", "account-1");
        request.Headers.Add("X-Internal-Key", InternalKey);
        return request;
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "PersonalFinance.ApiService";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>The real ApiService pipeline on an InMemory database, in the given environment.</summary>
    private sealed class ApiServiceHost(string environment, bool seed) : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"InMemory:ResearchFixtures_{Guid.NewGuid():N}");
            builder.UseSetting("Internal:ApiKey", InternalKey);
            builder.UseSetting(ResearchFixtureSeeder.Setting, seed ? "true" : "false");
        }
    }
}
