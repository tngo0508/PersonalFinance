using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// The Journey endpoints refuse anonymous callers, unlike the Library beside them, and serve a signed-in Account
/// its own progress.
/// </summary>
[Collection("ApiServiceHost")]
public class JourneyEndpointTests : IClassFixture<LearningEndpointTests.FixtureContentHost>
{
    private const string SharedSecret = "learning-test-shared-secret";

    private readonly LearningEndpointTests.FixtureContentHost _host;

    public JourneyEndpointTests(LearningEndpointTests.FixtureContentHost host)
    {
        _host = host;
    }

    [Theory]
    [InlineData("GET", "/api/learning/journey?language=en")]
    [InlineData("POST", "/api/learning/journey/lessons/build-a-cushion/finish?language=en")]
    [InlineData("GET", "/api/learning/journey/review?language=en")]
    [InlineData("POST", "/api/learning/journey/review/emergency-fund")]
    [InlineData("PUT", "/api/learning/journey/dashboard")]
    public async Task ProgressEndpoints_Anonymous_AreRefused(string method, string path)
    {
        using HttpClient client = Anonymous();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "GET")
        {
            request.Content = JsonContent.Create(new { });
        }

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Journey_SignedIn_FinishesDayOne_ThenLocksDayTwoUntilMidnight_AndRefusesItMeanwhile()
    {
        using HttpClient client = SignedIn($"journey-{Guid.NewGuid():N}");

        JourneyStateDto? fresh = await client.GetFromJsonAsync<JourneyStateDto>("/api/learning/journey?language=en");
        Assert.Equal([LessonStatus.Open, LessonStatus.Locked], fresh!.Lessons.Select(l => l.Status));

        using HttpResponseMessage finished = await client.PostAsJsonAsync(
            "/api/learning/journey/lessons/build-a-cushion/finish?language=vi",
            new FinishLessonRequest { TimeZone = "Asia/Ho_Chi_Minh" });
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);
        JourneyStateDto? after = await finished.Content.ReadFromJsonAsync<JourneyStateDto>();
        Assert.True(after!.Started);
        Assert.Equal([LessonStatus.Finished, LessonStatus.Locked], after.Lessons.Select(l => l.Status));
        Assert.NotNull(after.Lessons[1].OpensAtUtc);
        Assert.Equal("Vay tiền khôn ngoan", after.Lessons[1].Title);

        using HttpResponseMessage locked = await client.PostAsJsonAsync(
            "/api/learning/journey/lessons/borrow-wisely/finish?language=en", new FinishLessonRequest());
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        ApiErrorDto? error = await locked.Content.ReadFromJsonAsync<ApiErrorDto>();
        Assert.Equal(ApiErrorKeys.Learning.LessonLocked, Assert.Single(error!.Errors).Key);

        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/learning/journey/lessons/own-a-piece/finish?language=en", new FinishLessonRequest());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Review_SignedIn_ServesDayOnesCards_AndMarksThem()
    {
        using HttpClient client = SignedIn($"review-{Guid.NewGuid():N}");
        using HttpResponseMessage finished = await client.PostAsJsonAsync(
            "/api/learning/journey/lessons/build-a-cushion/finish?language=en",
            new FinishLessonRequest { TimeZone = "Asia/Ho_Chi_Minh" });
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);

        ReviewDto? review = await client.GetFromJsonAsync<ReviewDto>("/api/learning/journey/review?language=en");
        Assert.Equal(["emergency-fund", "compound-interest"], review!.Cards.Select(c => c.Slug));

        using HttpResponseMessage marked = await client.PostAsJsonAsync(
            "/api/learning/journey/review/emergency-fund", new MarkReviewCardRequest { KnewIt = true });
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);

        using HttpResponseMessage untaught = await client.PostAsJsonAsync(
            "/api/learning/journey/review/apr", new MarkReviewCardRequest { KnewIt = true });
        Assert.Equal(HttpStatusCode.NotFound, untaught.StatusCode);
    }

    [Fact]
    public async Task Dashboard_SignedIn_HidesAndShowsTheCard()
    {
        using HttpClient client = SignedIn($"dashboard-{Guid.NewGuid():N}");

        using HttpResponseMessage hidden = await client.PutAsJsonAsync(
            "/api/learning/journey/dashboard", new ShowOnDashboardRequest { Show = false });
        Assert.Equal(HttpStatusCode.NoContent, hidden.StatusCode);
        JourneyStateDto? state = await client.GetFromJsonAsync<JourneyStateDto>("/api/learning/journey?language=en");
        Assert.False(state!.ShowOnDashboard);

        using HttpResponseMessage shown = await client.PutAsJsonAsync(
            "/api/learning/journey/dashboard", new ShowOnDashboardRequest { Show = true });
        Assert.Equal(HttpStatusCode.NoContent, shown.StatusCode);
        state = await client.GetFromJsonAsync<JourneyStateDto>("/api/learning/journey?language=en");
        Assert.True(state!.ShowOnDashboard);
    }

    [Fact]
    public async Task Library_BesideTheJourney_StaysAnonymous()
    {
        using HttpClient client = Anonymous();

        using HttpResponseMessage response = await client.GetAsync("/api/learning/library?language=en");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient Anonymous() =>
        _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient SignedIn(string userId)
    {
        HttpClient client = Anonymous();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add(XUserIdAuthenticationDefaults.InternalKeyHeader, SharedSecret);
        return client;
    }
}
