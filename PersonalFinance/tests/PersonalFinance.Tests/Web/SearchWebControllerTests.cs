using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Search;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using Refit;
using Xunit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// The Web search endpoint behind the top-bar Global Search. A failure must reach the browser as an error status, so
/// the dropdown shows the Unavailable State, and must never carry exception details.
/// </summary>
public class SearchWebControllerTests
{
    private sealed class FakeSearchApi : ISearchApi
    {
        public Exception? Failure { get; init; }
        public SearchResultsDto Results { get; init; } = new();
        public string? LastQuery { get; private set; }

        public Task<SearchResultsDto> SearchAsync(string q, CancellationToken cancellationToken = default)
        {
            LastQuery = q;
            return Failure is null ? Task.FromResult(Results) : Task.FromException<SearchResultsDto>(Failure);
        }
    }

    private static SearchController CreateController(ISearchApi api) => new(api)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task Get_ReturnsResultsFromApi()
    {
        var results = new SearchResultsDto { SavingsGoals = [new SearchResultItemDto { Title = "Car" }] };
        var api = new FakeSearchApi { Results = results };

        IActionResult result = await CreateController(api).Get("car");

        Assert.Same(results, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("car", api.LastQuery);
    }

    [Fact]
    public async Task Get_WhenApiServiceFails_ReturnsErrorStatusWithoutExceptionDetails()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Search?q=car");
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("System.InvalidOperationException: secret stack trace", Encoding.UTF8,
                "text/plain")
        };
        ApiException failure = await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings());
        SearchController controller = CreateController(new FakeSearchApi { Failure = failure });

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Get("car"));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        string body = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("stack trace", body);
    }

    [Fact]
    public async Task Get_WhenApiServiceUnreachable_ReturnsBadGatewayWithGenericMessage()
    {
        SearchController controller = CreateController(new FakeSearchApi { Failure = new HttpRequestException("connection refused") });

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Get("car"));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
        string body = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.Contains(JsonApiErrorsFilter.UnavailableMessage, body);
        Assert.DoesNotContain("connection refused", body);
    }
}
