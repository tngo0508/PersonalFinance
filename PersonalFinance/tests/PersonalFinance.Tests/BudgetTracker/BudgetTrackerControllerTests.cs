using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Features.BudgetTracker.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// Unit tests for <see cref="BudgetTrackerController"/>: HTTP status codes, validation enforcement, error handling, authorization, and claims extraction.
/// </summary>
public class BudgetTrackerControllerTests
{
    private const string UserId = "test-api-user";

    private static (BudgetTrackerController controller, AppDbContext db) CreateController(string? userId = UserId)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BudgetTrackerController_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        var service = new MonthlyBudgetService(db);
        var dataSourceService = new BudgetDataSourceService(db);

        var controller = new BudgetTrackerController(
            service,
            dataSourceService,
            new InitializeMonthRequestValidator(),
            new SetPlannedRequestValidator(),
            new SetActualOverrideRequestValidator(),
            new SaveCategoryRequestValidator(),
            new SaveTransactionRequestValidator(),
            new SetBudgetDataSourceRequestValidator(),
            new BudgetTrackerRemovalService(db, dataSourceService, TimeProvider.System)
        );

        var httpContext = new DefaultHttpContext();
        if (userId != null)
        {
            Claim[] claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, db);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        AuthorizeAttribute? attribute = typeof(BudgetTrackerController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
    }

    [Fact]
    public async Task GetSheet_WithoutUserIdentity_Throws()
    {
        // [Authorize] stops anonymous requests before the action runs; reaching it without an Account is a bug.
        (BudgetTrackerController? controller, AppDbContext? db) = CreateController(userId: null);
        await using (db)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => controller.GetSheet(2026, 10));
        }
    }

    [Fact]
    public async Task GetSheet_Returns200WithSheetDto()
    {
        (BudgetTrackerController? controller, AppDbContext? db) = CreateController();
        await using (db)
        {
            IActionResult result = await controller.GetSheet(2026, 10);
            OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
            MonthlyBudgetSheetDto sheet = Assert.IsType<MonthlyBudgetSheetDto>(okResult.Value);

            Assert.Equal(2026, sheet.Year);
            Assert.Equal(10, sheet.Month);
            Assert.Equal("October 2026", sheet.MonthName);
        }
    }

    [Fact]
    public async Task GetSheet_WithInvalidMonth_RejectsWithTheInvalidMonthKeyAs400()
    {
        // The global ApiErrorExceptionFilter renders the exception as a 400 ApiErrorDto (ADR 0017).
        (BudgetTrackerController? controller, AppDbContext? db) = CreateController();
        await using (db)
        {
            ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(() => controller.GetSheet(2026, 13));

            Assert.Equal(ApiErrorKeys.BudgetTracker.InvalidYearOrMonth, ex.Key);
            Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        }
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_RejectsWithTheDuplicateKeyAs409()
    {
        (BudgetTrackerController? controller, AppDbContext? db) = CreateController();
        await using (db)
        {
            // First creation
            IActionResult result1 = await controller.CreateCategory(new SaveCategoryRequest
            {
                Name = "Freelance",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 1
            });
            Assert.IsType<ObjectResult>(result1);
            Assert.Equal(StatusCodes.Status201Created, ((ObjectResult)result1).StatusCode);

            // Duplicate creation: the service's rejection reaches the filter uncaught, as a 409
            ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(() =>
                controller.CreateCategory(new SaveCategoryRequest
                {
                    Name = "freelance",
                    Kind = BudgetCategoryKind.Income,
                    SortOrder = 2
                }));
            Assert.Equal(ApiErrorKeys.BudgetTracker.DuplicateCategory, ex.Key);
            Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        }
    }

    [Fact]
    public async Task UpdateCategory_WithKindChange_RejectsWithTheImmutableKindKeyAs400()
    {
        (BudgetTrackerController? controller, AppDbContext? db) = CreateController();
        await using (db)
        {
            IActionResult created = await controller.CreateCategory(new SaveCategoryRequest
            {
                Name = "Freelance",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 1
            });
            var createdDto = (BudgetCategoryDto)((ObjectResult)created).Value!;

            // Attempt to change kind to Expense
            ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(() =>
                controller.UpdateCategory(createdDto.Id, new SaveCategoryRequest
                {
                    Name = "Freelance",
                    Kind = BudgetCategoryKind.Expense,
                    SortOrder = 1
                }));

            Assert.Equal(ApiErrorKeys.BudgetTracker.CategoryKindImmutable, ex.Key);
            Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        }
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidAmount_Returns400WithTheKeyedBody()
    {
        (BudgetTrackerController? controller, AppDbContext? db) = CreateController();
        await using (db)
        {
            // Amount with 3 decimals is invalid
            IActionResult result = await controller.CreateTransaction(new SaveTransactionRequest
            {
                Date = new DateOnly(2026, 10, 15),
                Amount = 10.555m,
                Description = "Invalid amount",
                CategoryId = 1
            });

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
            ApiErrorDto body = Assert.IsType<ApiErrorDto>(badRequest.Value);
            ApiErrorEntryDto entry = Assert.Single(body.Errors);
            Assert.Equal(ApiErrorKeys.BudgetTracker.TransactionAmountWholeCents, entry.Key);
            Assert.Equal(nameof(SaveTransactionRequest.Amount), entry.Field);
        }
    }

    [Fact]
    public async Task DataSource_GetAndSetPreference_ReturnsExpectedValues()
    {
        (BudgetTrackerController? controller, AppDbContext? db) = CreateController();
        await using (db)
        {
            // Default when no snapshots exist is BudgetTracker
            IActionResult getResult1 = await controller.GetDataSource();
            OkObjectResult okResult1 = Assert.IsType<OkObjectResult>(getResult1);
            BudgetDataSourceDto dto1 = Assert.IsType<BudgetDataSourceDto>(okResult1.Value);
            Assert.Equal(BudgetDataSource.BudgetTracker, dto1.Source);
            Assert.True(dto1.IsDefault);

            // Explicitly set to GoogleSheet
            IActionResult setResult = await controller.SetDataSource(new SetBudgetDataSourceRequest
            { Source = BudgetDataSource.GoogleSheet });
            OkObjectResult okSetResult = Assert.IsType<OkObjectResult>(setResult);
            BudgetDataSourceDto dtoSet = Assert.IsType<BudgetDataSourceDto>(okSetResult.Value);
            Assert.Equal(BudgetDataSource.GoogleSheet, dtoSet.Source);
            Assert.False(dtoSet.IsDefault);

            // Get again
            IActionResult getResult2 = await controller.GetDataSource();
            OkObjectResult okResult2 = Assert.IsType<OkObjectResult>(getResult2);
            BudgetDataSourceDto dto2 = Assert.IsType<BudgetDataSourceDto>(okResult2.Value);
            Assert.Equal(BudgetDataSource.GoogleSheet, dto2.Source);
            Assert.False(dto2.IsDefault);
        }
    }
}
