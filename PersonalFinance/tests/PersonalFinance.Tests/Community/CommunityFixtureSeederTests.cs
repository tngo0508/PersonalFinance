using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Community;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Guild Hall E2E fixture (<see cref="CommunityFixtureSeeder"/>): on only in Development with
/// <c>E2E_SEED_COMMUNITY=true</c>, it stores one daily Challenge that is running for every viewer, and adds no second
/// one while it still runs.
/// </summary>
public class CommunityFixtureSeederTests
{
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("Development", "true", true)]
    [InlineData("Development", null, false)]
    [InlineData("Development", "false", false)]
    [InlineData("Production", "true", false)]
    [InlineData("Staging", "true", false)]
    public void IsEnabled_OnlyInDevelopmentWithTheSettingOn(string environment, string? setting, bool expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CommunityFixtureSeeder.Setting] = setting })
            .Build();

        Assert.Equal(expected, CommunityFixtureSeeder.IsEnabled(new Environment(environment), configuration));
    }

    [Fact]
    public async Task Seed_StoresARunningChallenge_OnceWhileItRuns_AndAgainAfterItEnds()
    {
        await using AppDbContext db = CreateContext();

        await CommunityFixtureSeeder.SeedAsync(db, _clock);
        await CommunityFixtureSeeder.SeedAsync(db, _clock);

        CommunityChallenge stored = await db.CommunityChallenges.SingleAsync();
        Assert.Equal(CommunityFixtureSeeder.Title, stored.Title);
        CommunityChallengeDto seen = Assert.Single(
            (await new CommunityService(db, _clock, NullLogger<CommunityService>.Instance).GetCommunityAsync("viewer"))
            .Challenges);
        Assert.NotNull(seen.CurrentPeriodStart);

        _clock.Advance(TimeSpan.FromDays(28));
        await CommunityFixtureSeeder.SeedAsync(db, _clock);

        Assert.Equal(2, await db.CommunityChallenges.CountAsync());
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CommunityFixture_{Guid.NewGuid():N}")
            .Options);

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "PersonalFinance.ApiService";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
