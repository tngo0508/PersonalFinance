using System.Net;
using System.Text;
using PersonalFinance.Web.Infrastructure;
using Refit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// Page actions show ApiService's message for a rejected request, a generic "unavailable" message when it cannot be
/// reached, and their own fallback otherwise; never exception text.
/// </summary>
public class ApiErrorMessagesForUserTests
{
    [Fact]
    public async Task RejectedRequest_ShowsApiServiceMessage()
    {
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.Conflict, """{"error":"Category 'Food' already exists."}""");

        Assert.Equal("Category 'Food' already exists.", ApiErrorMessages.ForUser(ex, "fallback"));
    }

    [Fact]
    public async Task ApiServiceServerError_ShowsFallbackNotItsBody()
    {
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.InternalServerError, """{"error":"NullReferenceException at ..."}""");

        Assert.Equal("fallback", ApiErrorMessages.ForUser(ex, "fallback"));
    }

    [Fact]
    public void UnreachableApiService_ShowsUnavailableMessage()
    {
        Assert.Equal(JsonApiErrorsFilter.UnavailableMessage,
            ApiErrorMessages.ForUser(new HttpRequestException("connection refused"), "fallback"));
    }

    [Fact]
    public void AnyOtherException_ShowsFallbackNotExceptionText()
    {
        Assert.Equal("fallback",
            ApiErrorMessages.ForUser(new InvalidOperationException("Sequence contains no elements"), "fallback"));
    }

    private static async Task<ApiException> CreateApiExceptionAsync(HttpStatusCode statusCode, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.local/test");
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }
}
