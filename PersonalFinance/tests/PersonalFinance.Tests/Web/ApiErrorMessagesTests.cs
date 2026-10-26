using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.Infrastructure;
using Refit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// Web renders a keyed <see cref="ApiErrorDto"/> in the reader's language (ADR 0017), formats its arguments for the
/// reader's culture, and still shows a readable message for bodies that are not keyed yet.
/// </summary>
public class ApiErrorMessagesTests
{
    private const string WholeCents = "MoneyPriorities.Validation.WholeCents";
    private const string MonthlyAmountRange = "MoneyPriorities.Validation.MonthlyAmountRange";
    private const string CommentTooLong = "Community.Message.CommentTooLong";

    private static IStringLocalizer L => TestLocalization.Localizer;

    [Fact]
    public async Task KeyedBody_ResolvesInVietnamese()
    {
        using var ui = new UiCultureScope("vi");
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.BadRequest,
            $$"""{"errors":[{"key":"{{WholeCents}}","message":"EN"}]}""");

        Assert.True(TestLocalization.Catalog.TryGet("vi", WholeCents, out string text));
        Assert.Equal(text, ApiErrorMessages.FromApiException(L, ex));
    }

    [Fact]
    public async Task UnknownKey_FallsBackToTheEnglishMessage()
    {
        using var ui = new UiCultureScope("vi");
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.BadRequest,
            """{"errors":[{"key":"Nope.Nope","message":"EN"}]}""");

        Assert.Equal("EN", ApiErrorMessages.FromApiException(L, ex));
    }

    [Fact]
    public void KeyInEnglishOnly_FallsBackToEnglishText()
    {
        IStringLocalizer localizer = LocalizationTestCatalog.Localizer("""{"X.Error.Y":"English only"}""", "{}");
        using var ui = new UiCultureScope("vi");

        string message = ApiErrorMessages.Resolve(localizer, new ApiErrorEntryDto { Key = "X.Error.Y", Message = "EN" });

        Assert.Equal("English only", message);
    }

    [Fact]
    public void MoneyArg_FormatsInTheRequestCulture()
    {
        using var ui = new UiCultureScope("vi");
        CultureInfo viVn = CultureInfo.GetCultureInfo("vi-VN");
        string message = WithCulture(viVn, () => ApiErrorMessages.Resolve(L, new ApiErrorEntryDto
        {
            Key = MonthlyAmountRange,
            Message = "EN",
            Args = new Dictionary<string, ApiErrorArgDto>
            {
                ["min"] = new() { Kind = ApiErrorArgKind.Money, Value = "0", Currency = "USD" },
                ["max"] = new() { Kind = ApiErrorArgKind.Money, Value = "1000000", Currency = "USD" }
            }
        }));

        Assert.Contains(MoneyFormatter.FormatWhole(1_000_000m, "USD", viVn), message);
        Assert.DoesNotContain("{", message);
    }

    [Theory]
    [InlineData("en-US", "en", "2,000")]
    [InlineData("vi-VN", "vi", "2.000")]
    public void NumberArg_FormatsWithGroupSeparators(string culture, string uiCulture, string expected)
    {
        using var ui = new UiCultureScope(uiCulture);
        string message = WithCulture(CultureInfo.GetCultureInfo(culture),
            () => ApiErrorMessages.Resolve(L, Entry(CommentTooLong, ApiErrorArgKind.Number, "2000")));

        Assert.Contains(expected, message);
        Assert.DoesNotContain("{max}", message);
    }

    [Fact]
    public void UnparsableNumberArg_IsShownAsGiven()
    {
        using var ui = new UiCultureScope("en");
        string message = WithCulture(CultureInfo.GetCultureInfo("en-US"),
            () => ApiErrorMessages.Resolve(L, Entry(CommentTooLong, ApiErrorArgKind.Number, "abc")));

        Assert.Contains("abc", message);
    }

    [Fact]
    public void TextArg_IsInsertedUnencoded_EncodingIsTheViewsJob()
    {
        using var ui = new UiCultureScope("en");

        string message = ApiErrorMessages.Resolve(L, Entry(CommentTooLong, ApiErrorArgKind.Text, "<b>x</b>"));

        Assert.Contains("<b>x</b>", message);
    }

    [Fact]
    public async Task ProblemDetailsBody_ShowsInvalidRequest()
    {
        using var ui = new UiCultureScope("en");
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.BadRequest,
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"MonthlyAmount":["The value 'x' is not valid."]}}""");

        string message = ApiErrorMessages.FromApiException(L, ex);

        Assert.True(TestLocalization.Catalog.TryGet("en", "Errors.InvalidRequest", out string text));
        Assert.Equal(text, message);
        Assert.DoesNotContain("'x'", message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Errors.NotFound")]
    [InlineData(HttpStatusCode.Conflict, "Errors.Conflict")]
    public async Task ProblemDetailsBody_PicksTheKeyFromTheStatusCode(HttpStatusCode status, string key)
    {
        using var ui = new UiCultureScope("en");
        ApiException ex = await CreateApiExceptionAsync(status,
            $$"""{"status":{{(int)status}},"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found"}""");

        Assert.True(TestLocalization.Catalog.TryGet("en", key, out string text));
        Assert.Equal(text, ApiErrorMessages.FromApiException(L, ex));
    }

    [Fact]
    public async Task KeyedBodyWithNullArgOrMessage_NeverThrows()
    {
        using var ui = new UiCultureScope("en");
        ApiException nullArg = await CreateApiExceptionAsync(HttpStatusCode.BadRequest,
            $$"""{"errors":[{"key":"{{CommentTooLong}}","message":"EN","args":{"max":null} }]}""");
        ApiException nullMessage = await CreateApiExceptionAsync(HttpStatusCode.BadRequest,
            """{"errors":[{"key":"Nope.Nope","message":null}]}""");

        string withNullArg = WithCulture(CultureInfo.GetCultureInfo("en-US"), () => ApiErrorMessages.FromApiException(L, nullArg));
        string withNullMessage = ApiErrorMessages.FromApiException(L, nullMessage);

        Assert.Contains("{max}", withNullArg);
        Assert.Equal(string.Empty, withNullMessage);
        Assert.Equal(string.Empty, ApiErrorMessages.Resolve(L, new ApiErrorEntryDto { Key = "Nope.Nope", Message = null! }));
    }

    [Fact]
    public async Task LegacyErrorBody_StillShowsItsText()
    {
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.Conflict, """{"error":"Category 'Food' already exists."}""");

        Assert.Equal("Category 'Food' already exists.", ApiErrorMessages.FromApiException(L, ex));
    }

    [Fact]
    public async Task Fields_GroupsByField_AndPutsWholeRequestErrorsUnderEmptyKey()
    {
        using var ui = new UiCultureScope("en");
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.BadRequest, """
            {"errors":[
              {"key":"Nope.A","message":"Amount bad.","field":"MonthlyAmount"},
              {"key":"Nope.B","message":"Amount worse.","field":"MonthlyAmount"},
              {"key":"Nope.C","message":"Name bad.","field":"Name"},
              {"key":"Nope.D","message":"Whole request bad."}
            ]}
            """);

        IReadOnlyDictionary<string, IReadOnlyList<string>> fields = ApiErrorMessages.Fields(L, ex);

        Assert.Equal(["Amount bad.", "Amount worse."], fields["MonthlyAmount"]);
        Assert.Equal(["Name bad."], fields["Name"]);
        Assert.Equal(["Whole request bad."], fields[""]);
        Assert.Equal(3, fields.Count);
    }

    [Fact]
    public async Task Fields_WhenTheBodyIsNotKeyed_IsEmpty()
    {
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.Conflict, """{"error":"Category 'Food' already exists."}""");

        Assert.Empty(ApiErrorMessages.Fields(L, ex));
    }

    [Fact]
    public void JsonApiErrorsFilter_UnreachableApi_AnswersLocalizedUnavailable()
    {
        using var ui = new UiCultureScope("vi");

        ObjectResult result = RunFilter(new HttpRequestException("connection refused"));

        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        // vi.json gains Errors.* in a later task; until then the chain answers English, after it Vietnamese.
        Assert.Equal(L["Errors.Unavailable"].Value, ErrorOf(result));
        Assert.NotEqual("Errors.Unavailable", ErrorOf(result));
    }

    [Fact]
    public async Task JsonApiErrorsFilter_KeepsTheStatusCode()
    {
        using var ui = new UiCultureScope("vi");
        ApiException ex = await CreateApiExceptionAsync(HttpStatusCode.NotFound,
            $$"""{"errors":[{"key":"{{WholeCents}}","message":"EN"}]}""");

        ObjectResult result = RunFilter(ex);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal(TestLocalization.Vietnamese(WholeCents), ErrorOf(result));
    }

    [Fact]
    public void TryParse_AcceptsOnlyArraysOfKeyedEntries()
    {
        Assert.True(ApiErrorMessages.TryParse("""{"errors":[{"key":"A.B","message":"m"}]}""", out ApiErrorDto? dto));
        Assert.Equal("A.B", Assert.Single(dto!.Errors).Key);

        Assert.False(ApiErrorMessages.TryParse("""{"errors":["plain"]}""", out _));
        Assert.False(ApiErrorMessages.TryParse("""{"errors":{"Amount":["bad"]}}""", out _));
        Assert.False(ApiErrorMessages.TryParse("""{"error":"x"}""", out _));
        Assert.False(ApiErrorMessages.TryParse("<html>", out _));
        Assert.False(ApiErrorMessages.TryParse(null, out _));
    }

    private static ApiErrorEntryDto Entry(string key, ApiErrorArgKind kind, string value) => new()
    {
        Key = key,
        Message = "EN",
        Args = new Dictionary<string, ApiErrorArgDto> { ["max"] = new() { Kind = kind, Value = value } }
    };

    private static string WithCulture(CultureInfo culture, Func<string> action)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static ObjectResult RunFilter(Exception exception)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(actionContext, []) { Exception = exception };

        new JsonApiErrorsFilter(NullLogger<JsonApiErrorsFilter>.Instance, L).OnException(context);

        Assert.True(context.ExceptionHandled);
        return Assert.IsType<ObjectResult>(context.Result);
    }

    private static string? ErrorOf(ObjectResult result) =>
        result.Value!.GetType().GetProperty("error")!.GetValue(result.Value) as string;

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
