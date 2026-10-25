using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.BudgetTracker;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace PersonalFinance.Tests.BudgetTracker;

public class BudgetTrackerWebControllerTests
{
    private static BudgetTrackerController CreateController(IBudgetTrackerApi api,
        ILogger<BudgetTrackerController> logger)
    {
        var identity = new ClaimsIdentity(new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-123"),
            new(ClaimTypes.Name, "test-user-123@example.com")
        }, "TestAuthType");

        return new BudgetTrackerController(api, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static async Task<ApiException> CreateApiExceptionAsync(HttpStatusCode statusCode, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.local/budget-tracker");
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        AuthorizeAttribute? authAttr = typeof(BudgetTrackerController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
    }

    [Theory]
    [InlineData(nameof(BudgetTrackerController.InitializeMonth), typeof(HttpPostAttribute))]
    [InlineData(nameof(BudgetTrackerController.SetPlanned), typeof(HttpPutAttribute))]
    [InlineData(nameof(BudgetTrackerController.SetActualOverride), typeof(HttpPutAttribute))]
    [InlineData(nameof(BudgetTrackerController.CreateCategory), typeof(HttpPostAttribute))]
    [InlineData(nameof(BudgetTrackerController.UpdateCategory), typeof(HttpPutAttribute))]
    [InlineData(nameof(BudgetTrackerController.DeleteCategory), typeof(HttpDeleteAttribute))]
    [InlineData(nameof(BudgetTrackerController.CreateTransaction), typeof(HttpPostAttribute))]
    [InlineData(nameof(BudgetTrackerController.UpdateTransaction), typeof(HttpPutAttribute))]
    [InlineData(nameof(BudgetTrackerController.DeleteTransaction), typeof(HttpDeleteAttribute))]
    public void StateChangingActions_HaveAntiForgeryTokenAndHttpMethodAttributes(string actionName,
        Type httpMethodAttrType)
    {
        MethodInfo? method = typeof(BudgetTrackerController).GetMethod(actionName);
        Assert.NotNull(method);

        ValidateAntiForgeryTokenAttribute? antiforgeryAttr = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
        Assert.NotNull(antiforgeryAttr);

        Attribute? methodAttr = method.GetCustomAttribute(httpMethodAttrType);
        Assert.NotNull(methodAttr);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenArrayErrors_ExtractsAllMessages()
    {
        string json = """{"errors": ["Year must be between 1900 and 2100.", "Month must be between 1 and 12."]}""";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.BadRequest, json);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("Year must be between 1900 and 2100.; Month must be between 1 and 12.", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenObjectDictionaryErrors_PrefersErrorsOverTitle()
    {
        string json =
            """{"title": "One or more validation errors occurred.", "errors": {"Amount": ["Amount must be greater than 0."], "Date": ["Date is required."]}}""";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.BadRequest, json);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Contains("Amount must be greater than 0.", msg);
        Assert.Contains("Date is required.", msg);
        Assert.DoesNotContain("One or more validation errors occurred.", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenSingleErrorProperty_ReturnsError()
    {
        string json = """{"error": "Category 'Dining' already exists."}""";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.Conflict, json);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("Category 'Dining' already exists.", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenDetailProperty_ReturnsDetail()
    {
        string json = """{"detail": "The specified monthly budget line was not found."}""";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.NotFound, json);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("The specified monthly budget line was not found.", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenTitleOnly_ReturnsTitle()
    {
        string json = """{"title": "Invalid Request Parameters"}""";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.BadRequest, json);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("Invalid Request Parameters", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_When500InternalServerError_ReturnsGenericServerError()
    {
        string json = """{"title": "Internal Server Error", "detail": "System.NullReferenceException at Service.cs:42"}""";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.InternalServerError, json);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("A server error occurred. Please try again.", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenNonJsonContent_ReturnsGenericErrorMessage()
    {
        string html = "<html><body>502 Bad Gateway</body></html>";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.BadGateway, html);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("A server error occurred. Please try again.", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenNonJson400_ReturnsGenericErrorMessage()
    {
        string text = "plain text bad request";
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.BadRequest, text);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("An error occurred while processing the request.", msg);
    }

    [Fact]
    public async Task GetApiErrorMessage_WhenEmptyContent404_ReturnsNotFoundMessage()
    {
        ApiException apiEx = await CreateApiExceptionAsync(HttpStatusCode.NotFound, string.Empty);

        string msg = ApiErrorMessages.FromApiException(apiEx);

        Assert.Equal("The requested resource was not found.", msg);
    }

    [Fact]
    public async Task InitializeMonth_WhenRequestIsNull_ReturnsBadRequest()
    {
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi(), new RecordingLogger<BudgetTrackerController>());
        IActionResult result = await controller.InitializeMonth(null!);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task SetPlanned_WhenRequestIsNull_ReturnsBadRequest()
    {
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi(), new RecordingLogger<BudgetTrackerController>());
        IActionResult result = await controller.SetPlanned(2026, 10, 1, null!);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task SetActualOverride_WhenRequestIsNull_ReturnsBadRequest()
    {
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi(), new RecordingLogger<BudgetTrackerController>());
        IActionResult result = await controller.SetActualOverride(2026, 10, 1, null!);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WhenRequestIsNull_ReturnsBadRequest()
    {
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi(), new RecordingLogger<BudgetTrackerController>());
        IActionResult result = await controller.CreateCategory(null!);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WhenRequestIsNull_ReturnsBadRequest()
    {
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi(), new RecordingLogger<BudgetTrackerController>());
        IActionResult result = await controller.UpdateCategory(1, null!);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_WhenRequestIsNull_ReturnsBadRequest()
    {
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi(), new RecordingLogger<BudgetTrackerController>());
        IActionResult result = await controller.CreateTransaction(null!);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task UpdateTransaction_WhenRequestIsNull_ReturnsBadRequest()
    {
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi(), new RecordingLogger<BudgetTrackerController>());
        IActionResult result = await controller.UpdateTransaction(1, null!);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task Index_WhenApiSucceeds_RendersViewWithSheetAndCategories()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Sheet = new MonthlyBudgetSheetDto
            {
                Year = 2026,
                Month = 10,
                MonthName = "October 2026",
                IsInitialized = true,
                Expenses = new SheetSectionDto
                {
                    TotalPlanned = 1500m,
                    TotalActual = 1200m,
                    TotalDiff = 300m,
                    Rows = new List<SheetRowDto>
                    {
                        new() { CategoryId = 1, Name = "Food", Planned = 500m, Actual = 400m, Diff = 100m }
                    }
                },
                Income = new SheetSectionDto
                {
                    TotalPlanned = 5000m,
                    TotalActual = 5200m,
                    TotalDiff = 200m,
                    Rows = new List<SheetRowDto>
                    {
                        new() { CategoryId = 2, Name = "Paycheck", Planned = 5000m, Actual = 5200m, Diff = 200m }
                    }
                }
            },
            Categories = new List<BudgetCategoryDto>
            {
                new() { Id = 1, Name = "Food", Kind = BudgetCategoryKind.Expense, SortOrder = 0 },
                new() { Id = 2, Name = "Paycheck", Kind = BudgetCategoryKind.Income, SortOrder = 0 }
            }
        };

        var logger = new RecordingLogger<BudgetTrackerController>();
        BudgetTrackerController controller = CreateController(fakeApi, logger);

        IActionResult result = await controller.Index(2026, 10);

        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        BudgetTrackerViewModel model = Assert.IsType<BudgetTrackerViewModel>(viewResult.Model);
        Assert.Equal(2026, model.Year);
        Assert.Equal(10, model.Month);
        Assert.NotNull(model.Sheet);
        Assert.Equal(1500m, model.Sheet!.Expenses.TotalPlanned);
        Assert.Equal(2, model.Categories.Count);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public async Task Index_WhenApiFails_RendersViewWithErrorMessage()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Failure = new HttpRequestException("Backend API unavailable")
        };

        var logger = new RecordingLogger<BudgetTrackerController>();
        BudgetTrackerController controller = CreateController(fakeApi, logger);

        IActionResult result = await controller.Index(2026, 10);

        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        BudgetTrackerViewModel model = Assert.IsType<BudgetTrackerViewModel>(viewResult.Model);
        Assert.Null(model.Sheet);
        Assert.NotNull(model.ErrorMessage);
        Assert.Contains(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Index_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var fakeApi = new FakeBudgetTrackerApi();
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Index(2026, 10, cts.Token));
    }

    [Fact]
    public async Task GetSheet_WhenApiThrowsApiException_ReturnsStatusCodeAndParsedErrorMessage()
    {
        string json = """{"error": "Monthly sheet not found."}""";
        var fakeApi = new FakeBudgetTrackerApi
        {
            Failure = await CreateApiExceptionAsync(HttpStatusCode.NotFound, json)
        };
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.GetSheet(2026, 10));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task InitializeMonth_WhenCalled_PassesRequestAndReturnsJson()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Sheet = new MonthlyBudgetSheetDto { Year = 2026, Month = 11, IsInitialized = true }
        };
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        var request = new InitializeMonthRequest { Year = 2026, Month = 11, CopyFromPrevious = true };
        IActionResult result = await controller.InitializeMonth(request);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        MonthlyBudgetSheetDto sheet = Assert.IsType<MonthlyBudgetSheetDto>(jsonResult.Value);
        Assert.Equal(11, sheet.Month);
        Assert.True(sheet.IsInitialized);
        Assert.NotNull(fakeApi.LastInitializeMonthRequest);
        Assert.Equal(2026, fakeApi.LastInitializeMonthRequest.Year);
        Assert.Equal(11, fakeApi.LastInitializeMonthRequest.Month);
        Assert.True(fakeApi.LastInitializeMonthRequest.CopyFromPrevious);
    }

    [Fact]
    public async Task SetPlanned_WhenCalled_PassesParametersAndReturnsJson()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Sheet = new MonthlyBudgetSheetDto { Year = 2026, Month = 10 }
        };
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        var request = new SetPlannedRequest { Amount = 750.50m };
        IActionResult result = await controller.SetPlanned(2026, 10, 42, request);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<MonthlyBudgetSheetDto>(jsonResult.Value);
        Assert.Equal(2026, fakeApi.LastSetPlannedYear);
        Assert.Equal(10, fakeApi.LastSetPlannedMonth);
        Assert.Equal(42, fakeApi.LastSetPlannedCategoryId);
        Assert.Equal(750.50m, fakeApi.LastSetPlannedRequest?.Amount);
    }

    [Fact]
    public async Task SetPlanned_WhenApiThrowsValidationErrorArray_ReturnsBadRequestWithParsedMessage()
    {
        string json =
            """{"errors": ["Amount must be greater than or equal to 0.", "Amount cannot exceed 2 decimal places."]}""";
        var fakeApi = new FakeBudgetTrackerApi
        {
            Failure = await CreateApiExceptionAsync(HttpStatusCode.BadRequest, json)
        };
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.SetPlanned(2026, 10, 1, new SetPlannedRequest { Amount = -10 }));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task SetActualOverride_WhenCalled_PassesParametersAndReturnsJson()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Sheet = new MonthlyBudgetSheetDto { Year = 2026, Month = 10 }
        };
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        var request = new SetActualOverrideRequest { Amount = 450.00m };
        IActionResult result = await controller.SetActualOverride(2026, 10, 7, request);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<MonthlyBudgetSheetDto>(jsonResult.Value);
        Assert.Equal(2026, fakeApi.LastSetActualOverrideYear);
        Assert.Equal(10, fakeApi.LastSetActualOverrideMonth);
        Assert.Equal(7, fakeApi.LastSetActualOverrideCategoryId);
        Assert.Equal(450.00m, fakeApi.LastSetActualOverrideRequest?.Amount);
    }

    [Fact]
    public async Task GetCategories_WhenCalled_ReturnsJsonCategories()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Categories = new List<BudgetCategoryDto>
            {
                new() { Id = 1, Name = "Rent", Kind = BudgetCategoryKind.Expense, SortOrder = 1 }
            }
        };
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        IActionResult result = await controller.GetCategories(false);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        List<BudgetCategoryDto> categories = Assert.IsType<List<BudgetCategoryDto>>(jsonResult.Value);
        Assert.Single(categories);
    }

    [Fact]
    public async Task CreateCategory_WhenCalled_PassesRequestAndReturnsCreatedCategory()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        var request = new SaveCategoryRequest { Name = "Utilities", Kind = BudgetCategoryKind.Expense, SortOrder = 2 };
        IActionResult result = await controller.CreateCategory(request);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        BudgetCategoryDto created = Assert.IsType<BudgetCategoryDto>(jsonResult.Value);
        Assert.Equal("Utilities", created.Name);
        Assert.Equal(BudgetCategoryKind.Expense, created.Kind);
        Assert.NotNull(fakeApi.LastCreateCategoryRequest);
        Assert.Equal("Utilities", fakeApi.LastCreateCategoryRequest.Name);
    }

    [Fact]
    public async Task CreateCategory_WhenDuplicate_ReturnsConflictStatus()
    {
        string json = """{"error": "Category with name 'Utilities' already exists."}""";
        var fakeApi = new FakeBudgetTrackerApi
        {
            Failure = await CreateApiExceptionAsync(HttpStatusCode.Conflict, json)
        };
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.CreateCategory(new SaveCategoryRequest
        { Name = "Utilities", Kind = BudgetCategoryKind.Expense }));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WhenCalled_PassesIdAndRequestAndReturnsUpdatedCategory()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        var request = new SaveCategoryRequest
        { Name = "Utilities & Gas", Kind = BudgetCategoryKind.Expense, SortOrder = 2 };
        IActionResult result = await controller.UpdateCategory(5, request);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        BudgetCategoryDto updated = Assert.IsType<BudgetCategoryDto>(jsonResult.Value);
        Assert.Equal("Utilities & Gas", updated.Name);
        Assert.Equal(5, fakeApi.LastUpdateCategoryId);
        Assert.NotNull(fakeApi.LastUpdateCategoryRequest);
        Assert.Equal("Utilities & Gas", fakeApi.LastUpdateCategoryRequest.Name);
    }

    [Fact]
    public async Task DeleteCategory_WhenCalled_PassesIdAndReturnsNoContent()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        IActionResult result = await controller.DeleteCategory(5);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(5, fakeApi.LastDeleteCategoryId);
    }

    [Fact]
    public async Task CreateTransaction_WhenCalled_PassesRequestAndReturnsCreatedTransaction()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        var request = new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 85.50m,
            Description = "Groceries",
            CategoryId = 1
        };
        IActionResult result = await controller.CreateTransaction(request);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        BudgetTransactionDto tx = Assert.IsType<BudgetTransactionDto>(jsonResult.Value);
        Assert.Equal(85.50m, tx.Amount);
        Assert.Equal("Groceries", tx.Description);
        Assert.NotNull(fakeApi.LastCreateTransactionRequest);
        Assert.Equal(85.50m, fakeApi.LastCreateTransactionRequest.Amount);
    }

    [Fact]
    public async Task UpdateTransaction_WhenCalled_PassesIdAndRequestAndReturnsUpdatedTransaction()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        var request = new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 16),
            Amount = 95.00m,
            Description = "Groceries Updated",
            CategoryId = 1
        };
        IActionResult result = await controller.UpdateTransaction(12, request);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        BudgetTransactionDto tx = Assert.IsType<BudgetTransactionDto>(jsonResult.Value);
        Assert.Equal(95.00m, tx.Amount);
        Assert.Equal(12, fakeApi.LastUpdateTransactionId);
        Assert.NotNull(fakeApi.LastUpdateTransactionRequest);
    }

    [Fact]
    public async Task DeleteTransaction_WhenCalled_PassesIdAndReturnsNoContent()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        BudgetTrackerController controller = CreateController(fakeApi, new RecordingLogger<BudgetTrackerController>());

        IActionResult result = await controller.DeleteTransaction(12);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(12, fakeApi.LastDeleteTransactionId);
    }

    [Fact]
    public async Task GetDataSource_ReturnsActiveSource()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        var logger = new RecordingLogger<BudgetTrackerController>();
        BudgetTrackerController controller = CreateController(fakeApi, logger);

        IActionResult result = await controller.GetDataSource(CancellationToken.None);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        BudgetDataSourceDto dto = Assert.IsType<BudgetDataSourceDto>(jsonResult.Value);
        Assert.Equal(BudgetDataSource.GoogleSheet, dto.Source);
        Assert.True(dto.IsDefault);
    }

    [Fact]
    public async Task GetDataSource_WhenApiThrows_Returns500()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Failure = await CreateApiExceptionAsync(HttpStatusCode.InternalServerError,
                "{\"error\":\"Internal error\"}")
        };
        var logger = new RecordingLogger<BudgetTrackerController>();
        BudgetTrackerController controller = CreateController(fakeApi, logger);

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.GetDataSource(CancellationToken.None));

        ObjectResult objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task SetDataSource_WhenValid_ReturnsOkWithUpdatedSource()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        var logger = new RecordingLogger<BudgetTrackerController>();
        BudgetTrackerController controller = CreateController(fakeApi, logger);

        var request = new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker };
        IActionResult result = await controller.SetDataSource(request, CancellationToken.None);

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        BudgetDataSourceDto dto = Assert.IsType<BudgetDataSourceDto>(jsonResult.Value);
        Assert.Equal(BudgetDataSource.BudgetTracker, dto.Source);
        Assert.False(dto.IsDefault);
    }

    [Fact]
    public async Task SetDataSource_WhenNullRequest_ReturnsBadRequest()
    {
        var fakeApi = new FakeBudgetTrackerApi();
        var logger = new RecordingLogger<BudgetTrackerController>();
        BudgetTrackerController controller = CreateController(fakeApi, logger);

        IActionResult result = await controller.SetDataSource(null!, CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task SetDataSource_WhenApiThrows_ReturnsFormattedError()
    {
        var fakeApi = new FakeBudgetTrackerApi
        {
            Failure = await CreateApiExceptionAsync(HttpStatusCode.BadRequest, "{\"errors\":[\"Invalid data source\"]}")
        };
        var logger = new RecordingLogger<BudgetTrackerController>();
        BudgetTrackerController controller = CreateController(fakeApi, logger);

        var request = new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker };
        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.SetDataSource(request, CancellationToken.None));

        ObjectResult objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objResult.StatusCode);
        Assert.NotNull(objResult.Value);
    }
}

public class FakeBudgetTrackerApi : IBudgetTrackerApi
{
    public MonthlyBudgetSheetDto Sheet { get; set; } = new();
    public List<BudgetCategoryDto> Categories { get; set; } = new();
    public Exception? Failure { get; set; }

    public InitializeMonthRequest? LastInitializeMonthRequest { get; private set; }
    public int? LastSetPlannedYear { get; private set; }
    public int? LastSetPlannedMonth { get; private set; }
    public int? LastSetPlannedCategoryId { get; private set; }
    public SetPlannedRequest? LastSetPlannedRequest { get; private set; }

    public int? LastSetActualOverrideYear { get; private set; }
    public int? LastSetActualOverrideMonth { get; private set; }
    public int? LastSetActualOverrideCategoryId { get; private set; }
    public SetActualOverrideRequest? LastSetActualOverrideRequest { get; private set; }

    public SaveCategoryRequest? LastCreateCategoryRequest { get; private set; }
    public int? LastUpdateCategoryId { get; private set; }
    public SaveCategoryRequest? LastUpdateCategoryRequest { get; private set; }
    public int? LastDeleteCategoryId { get; private set; }

    public SaveTransactionRequest? LastCreateTransactionRequest { get; private set; }
    public int? LastUpdateTransactionId { get; private set; }
    public SaveTransactionRequest? LastUpdateTransactionRequest { get; private set; }
    public int? LastDeleteTransactionId { get; private set; }

    public Task<MonthlyBudgetSheetDto> GetSheetAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<MonthlyBudgetSheetDto>(cancellationToken);
        }

        return Failure is not null
            ? Task.FromException<MonthlyBudgetSheetDto>(Failure)
            : Task.FromResult(Sheet);
    }

    public Task<MonthlyBudgetSheetDto> InitializeMonthAsync(InitializeMonthRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<MonthlyBudgetSheetDto>(cancellationToken);
        }

        LastInitializeMonthRequest = request;
        return Failure is not null
            ? Task.FromException<MonthlyBudgetSheetDto>(Failure)
            : Task.FromResult(Sheet);
    }

    public Task<MonthlyBudgetSheetDto> SetPlannedAsync(int year, int month, int categoryId, SetPlannedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<MonthlyBudgetSheetDto>(cancellationToken);
        }

        LastSetPlannedYear = year;
        LastSetPlannedMonth = month;
        LastSetPlannedCategoryId = categoryId;
        LastSetPlannedRequest = request;
        return Failure is not null
            ? Task.FromException<MonthlyBudgetSheetDto>(Failure)
            : Task.FromResult(Sheet);
    }

    public Task<MonthlyBudgetSheetDto> SetActualOverrideAsync(int year, int month, int categoryId,
        SetActualOverrideRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<MonthlyBudgetSheetDto>(cancellationToken);
        }

        LastSetActualOverrideYear = year;
        LastSetActualOverrideMonth = month;
        LastSetActualOverrideCategoryId = categoryId;
        LastSetActualOverrideRequest = request;
        return Failure is not null
            ? Task.FromException<MonthlyBudgetSheetDto>(Failure)
            : Task.FromResult(Sheet);
    }

    public Task<List<BudgetCategoryDto>> GetCategoriesAsync(bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<List<BudgetCategoryDto>>(cancellationToken);
        }

        return Failure is not null
            ? Task.FromException<List<BudgetCategoryDto>>(Failure)
            : Task.FromResult(Categories);
    }

    public Task<BudgetCategoryDto> CreateCategoryAsync(SaveCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<BudgetCategoryDto>(cancellationToken);
        }

        LastCreateCategoryRequest = request;
        return Failure is not null
            ? Task.FromException<BudgetCategoryDto>(Failure)
            : Task.FromResult(new BudgetCategoryDto
            {
                Id = 99,
                Name = request.Name,
                Kind = request.Kind,
                SortOrder = request.SortOrder
            });
    }

    public Task<BudgetCategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<BudgetCategoryDto>(cancellationToken);
        }

        LastUpdateCategoryId = id;
        LastUpdateCategoryRequest = request;
        return Failure is not null
            ? Task.FromException<BudgetCategoryDto>(Failure)
            : Task.FromResult(new BudgetCategoryDto
            {
                Id = id,
                Name = request.Name,
                Kind = request.Kind,
                SortOrder = request.SortOrder
            });
    }

    public Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        LastDeleteCategoryId = id;
        return Failure is not null
            ? Task.FromException(Failure)
            : Task.CompletedTask;
    }

    public Task<BudgetTransactionDto> CreateTransactionAsync(SaveTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<BudgetTransactionDto>(cancellationToken);
        }

        LastCreateTransactionRequest = request;
        return Failure is not null
            ? Task.FromException<BudgetTransactionDto>(Failure)
            : Task.FromResult(new BudgetTransactionDto
            {
                Id = 101,
                Date = request.Date,
                Amount = request.Amount,
                Description = request.Description,
                CategoryId = request.CategoryId,
                CategoryName = "Test Category",
                Kind = BudgetCategoryKind.Expense
            });
    }

    public Task<BudgetTransactionDto> UpdateTransactionAsync(int id, SaveTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<BudgetTransactionDto>(cancellationToken);
        }

        LastUpdateTransactionId = id;
        LastUpdateTransactionRequest = request;
        return Failure is not null
            ? Task.FromException<BudgetTransactionDto>(Failure)
            : Task.FromResult(new BudgetTransactionDto
            {
                Id = id,
                Date = request.Date,
                Amount = request.Amount,
                Description = request.Description,
                CategoryId = request.CategoryId,
                CategoryName = "Test Category",
                Kind = BudgetCategoryKind.Expense
            });
    }

    public Task DeleteTransactionAsync(int id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        LastDeleteTransactionId = id;
        return Failure is not null
            ? Task.FromException(Failure)
            : Task.CompletedTask;
    }

    public Task<BudgetDataSourceDto> GetDataSourceAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<BudgetDataSourceDto>(cancellationToken);
        }

        return Failure is not null
            ? Task.FromException<BudgetDataSourceDto>(Failure)
            : Task.FromResult(new BudgetDataSourceDto { Source = BudgetDataSource.GoogleSheet, IsDefault = true });
    }

    public Task<BudgetDataSourceDto> SetDataSourceAsync(SetBudgetDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<BudgetDataSourceDto>(cancellationToken);
        }

        return Failure is not null
            ? Task.FromException<BudgetDataSourceDto>(Failure)
            : Task.FromResult(new BudgetDataSourceDto { Source = request.Source, IsDefault = false });
    }

    public List<BudgetDataSourceSummaryDto> DataSourceSummaries { get; set; } = new();
    public int RemoveDataCalls { get; private set; }

    public Task<List<BudgetDataSourceSummaryDto>> GetDataSourceSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DataSourceSummaries);
    }

    public Task<BudgetDataSourceSummaryDto> RemoveDataAsync(CancellationToken cancellationToken = default)
    {
        RemoveDataCalls++;
        return Task.FromResult(DataSourceSummaries.FirstOrDefault(s => s.Source == BudgetDataSource.BudgetTracker)
                               ?? new BudgetDataSourceSummaryDto { Source = BudgetDataSource.BudgetTracker });
    }
}
