using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// The Atelier E2E fixture (#520): on only in Development with <c>E2E_SEED_LEARNING=true</c>, the ApiService stores the
/// fixture learner past Lesson 1, so Lesson 2 and its Review are open, and the Web's fixture Account has the same id.
/// </summary>
public class LearningFixtureSeederTests
{
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("Development", "true", true)]
    [InlineData("Development", null, false)]
    [InlineData("Production", "true", false)]
    [InlineData("Staging", "true", false)]
    public void IsEnabled_OnlyInDevelopmentWithTheSettingOn(string environment, string? setting, bool expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [LearningFixtureSeeder.Setting] = setting })
            .Build();

        Assert.Equal(expected, LearningFixtureSeeder.IsEnabled(new Environment(environment), configuration));
        Assert.Equal(expected, LearningFixtureAccountSeeder.IsEnabled(new Environment(environment), configuration));
    }

    [Fact]
    public void TheWebAccount_IsTheApiServiceLearner()
    {
        Assert.Equal(LearningFixtureSeeder.UserId, LearningFixtureAccountSeeder.UserId);
        Assert.Equal(LearningFixtureSeeder.Setting, LearningFixtureAccountSeeder.Setting);
    }

    [Fact]
    public async Task Seed_OpensLessonTwo_WithLessonOnesCardsInTheReview_Once()
    {
        await using AppDbContext db = CreateContext();
        LearningContent content = LearningContent.Load(LearningContentFolders.Fixture);

        await LearningFixtureSeeder.SeedAsync(db, content, _clock);
        await LearningFixtureSeeder.SeedAsync(db, content, _clock);

        Assert.Equal(1, await db.LessonCompletions.CountAsync());
        var journey = new JourneyService(db, content, _clock);
        JourneyStateDto state = await journey.GetStateAsync(LearningFixtureSeeder.UserId, "en");
        Assert.Equal([LessonStatus.Finished, LessonStatus.Open], state.Lessons.Select(l => l.Status));
        ReviewDto review = await journey.GetReviewAsync(LearningFixtureSeeder.UserId, "en");
        Assert.Equal(["emergency-fund", "compound-interest"], review.Cards.Select(c => c.Slug));
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LearningFixture_{Guid.NewGuid():N}")
            .Options);

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "PersonalFinance.ApiService";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
