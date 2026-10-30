using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.Shared.DTOs.Learning;
using AdminController = PersonalFinance.ApiService.Features.Admin.AdminController;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// The Atelier's content endpoints answer anonymous callers: no shared secret and no Account, as a guest's page load
/// through Web sends.
/// </summary>
[Collection("ApiServiceHost")]
public class LearningEndpointTests : IClassFixture<LearningEndpointTests.FixtureContentHost>
{
    private readonly FixtureContentHost _host;

    public LearningEndpointTests(FixtureContentHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task Library_Anonymous_ListsEveryTopicWithItsEntries()
    {
        HttpResponseMessage response = await Client().GetAsync("/api/learning/library?language=en");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        LearningLibraryDto? library = await response.Content.ReadFromJsonAsync<LearningLibraryDto>();
        Assert.Equal(
            ["saving-banking", "borrowing-credit", "investing-basics", "trading", "tax", "retirement"],
            library!.Topics.Select(t => t.Slug));
        Assert.Equal(["apr", "credit-score"],
            library.Topics.Single(t => t.Slug == "borrowing-credit").Terms.Select(t => t.Slug));
    }

    [Fact]
    public async Task Term_Anonymous_InVietnamese_ServesTheTranslatedFields()
    {
        TermEntryDto? apr = await Client().GetFromJsonAsync<TermEntryDto>("/api/learning/terms/apr?language=vi");

        Assert.Equal("Lãi suất phần trăm hằng năm", apr!.Term);
        Assert.StartsWith("Ngân hàng", apr.InVietnam!.Text);
    }

    [Fact]
    public async Task Term_Unknown_IsNotFound()
    {
        HttpResponseMessage response = await Client().GetAsync("/api/learning/terms/mortgage");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Lessons_Anonymous_ServeTheOutlineAndALesson()
    {
        List<LessonSummaryDto>? outline =
            await Client().GetFromJsonAsync<List<LessonSummaryDto>>("/api/learning/lessons?language=en");
        LessonDto? lesson = await Client().GetFromJsonAsync<LessonDto>("/api/learning/lessons/build-a-cushion");

        Assert.Equal(["build-a-cushion", "borrow-wisely"], outline!.Select(l => l.Slug));
        Assert.Equal(["emergency-fund", "compound-interest"], lesson!.Terms.Select(t => t.Slug));
    }

    [Fact]
    public async Task Lesson_Unknown_IsNotFound()
    {
        HttpResponseMessage response = await Client().GetAsync("/api/learning/lessons/no-such-lesson");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient Client() =>
        _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>ApiService on InMemory, reading the fixture content folder.</summary>
    public sealed class FixtureContentHost : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"InMemory:LearningHttpTests_{Guid.NewGuid():N}");
            builder.UseSetting("Internal:ApiKey", "learning-test-shared-secret");
            builder.UseSetting(LearningFeature.ContentPathKey, LearningContentFolders.Fixture);
        }
    }
}
