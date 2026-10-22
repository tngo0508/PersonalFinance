using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.Budget;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Light checks of the Web <see cref="FeedbackController"/>: it forwards to <see cref="IFeedbackApi"/>, keeps the
/// user's input on failure, and never shows exception details.
/// </summary>
public class FeedbackWebControllerTests
{
    private static FeedbackController CreateController(IFeedbackApi api)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "feedback-user")], "TestAuthType");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new FeedbackController(api, NullLogger<FeedbackController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private static FeedbackViewModel ValidModel() => new()
    {
        Category = FeedbackCategory.Bug,
        Message = "The chart is blank.",
        PageUrl = "/Dashboard"
    };

    [Fact]
    public void Controller_RequiresASignedInAccount()
    {
        Assert.NotNull(typeof(FeedbackController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Send_IsAPostProtectedByAnAntiForgeryToken()
    {
        var send = typeof(FeedbackController).GetMethod(nameof(FeedbackController.Send))!;

        Assert.NotNull(send.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(send.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Theory]
    [InlineData("/Budget", "/Budget")]
    [InlineData("/Budget?month=10", "/Budget?month=10")]
    [InlineData("/Feedback/Send", null)]
    [InlineData("https://evil.example/phish", null)]
    [InlineData("//evil.example", null)]
    [InlineData(null, null)]
    public void Index_CapturesOnlyAnAppRelativePageTheAccountCameFrom(string? from, string? expected)
    {
        var result = CreateController(new FakeFeedbackApi()).Index(from);

        var model = Assert.IsType<FeedbackViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(expected, model.PageUrl);
    }

    [Fact]
    public async Task Send_ForwardsTheFeedback_ThenRedirectsWithAConfirmation()
    {
        var api = new FakeFeedbackApi();
        var controller = CreateController(api);

        var result = await controller.Send(ValidModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FeedbackController.Index), redirect.ActionName);
        Assert.NotNull(api.Submitted);
        Assert.Equal(FeedbackCategory.Bug, api.Submitted!.Category);
        Assert.Equal("The chart is blank.", api.Submitted.Message);
        Assert.Equal("/Dashboard", api.Submitted.PageUrl);
        Assert.Equal(FeedbackController.SentMessage, controller.TempData[FeedbackController.SentKey]);
    }

    [Fact]
    public async Task Send_WhenTheFormIsInvalid_KeepsTheInputAndDoesNotCallTheApi()
    {
        var api = new FakeFeedbackApi();
        var controller = CreateController(api);
        controller.ModelState.AddModelError(nameof(FeedbackViewModel.Message), "Message is required.");
        var model = ValidModel();

        var result = await controller.Send(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(FeedbackController.Index), view.ViewName);
        Assert.Same(model, view.Model);
        Assert.Null(api.Submitted);
    }

    [Fact]
    public async Task Send_WhenTheApiRejectsTheRequest_ShowsItsMessageAndKeepsTheInput()
    {
        var failure = await CreateApiExceptionAsync(HttpStatusCode.BadRequest,
            """{"errors":["Message must be 2,000 characters or fewer."]}""");
        var controller = CreateController(new FakeFeedbackApi { Failure = failure });
        var model = ValidModel();

        var result = await controller.Send(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("Message must be 2,000 characters or fewer.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{"error":"You've sent a lot of feedback recently. Please try again later."}""")]
    public async Task Send_WhenRateLimited_ShowsATryAgainLaterMessageAndKeepsTheInput(string content)
    {
        var failure = await CreateApiExceptionAsync(HttpStatusCode.TooManyRequests, content);
        var controller = CreateController(new FakeFeedbackApi { Failure = failure });
        var model = ValidModel();

        var result = await controller.Send(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(FeedbackController.Index), view.ViewName);
        Assert.Same(model, view.Model);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal(FeedbackMessages.RateLimited, error.ErrorMessage);
        Assert.Null(controller.TempData[FeedbackController.SentKey]);
    }

    [Fact]
    public async Task Send_WhenTheApiIsUnreachable_ShowsAFriendlyMessageWithoutExceptionDetails()
    {
        var controller = CreateController(new FakeFeedbackApi
        {
            Failure = new HttpRequestException("Connection refused (apiservice:8443)")
        });

        var result = await controller.Send(ValidModel());

        Assert.IsType<ViewResult>(result);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.DoesNotContain("Connection refused", error.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage));
    }

    [Fact]
    public async Task Send_WhenAnUnexpectedErrorOccurs_DoesNotHideIt()
    {
        var controller = CreateController(new FakeFeedbackApi { Failure = new InvalidOperationException("bug") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Send(ValidModel()));
    }

    [Fact]
    public async Task Send_WhenTheRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var controller = CreateController(new FakeFeedbackApi { Failure = new TaskCanceledException() });
        controller.HttpContext.RequestAborted = cts.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Send(ValidModel()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public void ViewModel_RejectsAnEmptyMessage(string message)
    {
        var model = ValidModel();
        model.Message = message;

        Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(FeedbackViewModel.Message)));
    }

    [Fact]
    public void ViewModel_RejectsAMessageOver2000Characters()
    {
        var model = ValidModel();
        model.Message = new string('a', 2001);

        var error = Assert.Single(Validate(model), r => r.MemberNames.Contains(nameof(FeedbackViewModel.Message)));
        Assert.Equal("Message must be 2,000 characters or fewer.", error.ErrorMessage);
    }

    [Fact]
    public void ViewModel_AcceptsAValidForm()
    {
        Assert.Empty(Validate(ValidModel()));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static async Task<ApiException> CreateApiExceptionAsync(HttpStatusCode statusCode, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.local/api/feedback");
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    private sealed class FakeFeedbackApi : IFeedbackApi
    {
        public Exception? Failure { get; init; }

        public SubmitFeedbackRequest? Submitted { get; private set; }

        public Task SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                return Task.FromException(Failure);
            }

            Submitted = request;
            return Task.CompletedTask;
        }

        public Task<List<FeedbackAdminItemDto>> ListAsync(FeedbackStatus? status = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The Admin inbox is not part of sending Feedback.");

        public Task ChangeStatusAsync(int id, ChangeFeedbackStatusRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Feedback Status triage is not part of sending Feedback.");
    }
}
