using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Controllers;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests;

/// <summary>
/// Unit tests for <see cref="BudgetTrackerController"/>: HTTP status codes, validation enforcement, error handling, authorization, and claims extraction.
/// </summary>
public class BudgetTrackerControllerTests
{
    private const string UserId = "test-api-user";

    private static (BudgetTrackerController controller, AppDbContext db) CreateController(string? userId = UserId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
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
            new SetBudgetDataSourceRequestValidator()
        );

        var httpContext = new DefaultHttpContext();
        if (userId != null)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
            httpContext.Request.Headers["X-User-Id"] = userId;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, db);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var attribute = typeof(BudgetTrackerController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
    }

    [Fact]
    public async Task GetSheet_WithoutUserIdentity_ThrowsUnauthorizedAccessException()
    {
        var (controller, db) = CreateController(userId: null);
        await using (db)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetSheet(2026, 10));
        }
    }

    [Fact]
    public async Task GetSheet_Returns200WithSheetDto()
    {
        var (controller, db) = CreateController();
        await using (db)
        {
            var result = await controller.GetSheet(2026, 10);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var sheet = Assert.IsType<MonthlyBudgetSheetDto>(okResult.Value);

            Assert.Equal(2026, sheet.Year);
            Assert.Equal(10, sheet.Month);
            Assert.Equal("October 2026", sheet.MonthName);
        }
    }

    [Fact]
    public async Task GetSheet_WithInvalidMonth_Returns400BadRequest()
    {
        var (controller, db) = CreateController();
        await using (db)
        {
            var result = await controller.GetSheet(2026, 13);
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_Returns409Conflict()
    {
        var (controller, db) = CreateController();
        await using (db)
        {
            // First creation
            var result1 = await controller.CreateCategory(new SaveCategoryRequest
            {
                Name = "Freelance",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 1
            });
            Assert.IsType<ObjectResult>(result1);
            Assert.Equal(StatusCodes.Status201Created, ((ObjectResult)result1).StatusCode);

            // Duplicate creation
            var result2 = await controller.CreateCategory(new SaveCategoryRequest
            {
                Name = "freelance",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 2
            });
            var conflict = Assert.IsType<ConflictObjectResult>(result2);
            Assert.NotNull(conflict.Value);
        }
    }

    [Fact]
    public async Task UpdateCategory_WithKindChange_Returns400BadRequest()
    {
        var (controller, db) = CreateController();
        await using (db)
        {
            var created = await controller.CreateCategory(new SaveCategoryRequest
            {
                Name = "Freelance",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 1
            });
            var createdDto = (BudgetCategoryDto)((ObjectResult)created).Value!;

            // Attempt to change kind to Expense
            var updateResult = await controller.UpdateCategory(createdDto.Id, new SaveCategoryRequest
            {
                Name = "Freelance",
                Kind = BudgetCategoryKind.Expense,
                SortOrder = 1
            });

            Assert.IsType<BadRequestObjectResult>(updateResult);
        }
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidAmount_Returns400BadRequest()
    {
        var (controller, db) = CreateController();
        await using (db)
        {
            // Amount with 3 decimals is invalid
            var result = await controller.CreateTransaction(new SaveTransactionRequest
            {
                Date = new DateOnly(2026, 10, 15),
                Amount = 10.555m,
                Description = "Invalid amount",
                CategoryId = 1
            });

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }

    [Fact]
    public async Task DataSource_GetAndSetPreference_ReturnsExpectedValues()
    {
        var (controller, db) = CreateController();
        await using (db)
        {
            // Default when no snapshots exist is BudgetTracker
            var getResult1 = await controller.GetDataSource();
            var okResult1 = Assert.IsType<OkObjectResult>(getResult1);
            var dto1 = Assert.IsType<BudgetDataSourceDto>(okResult1.Value);
            Assert.Equal(BudgetDataSource.BudgetTracker, dto1.Source);
            Assert.True(dto1.IsDefault);

            // Explicitly set to GoogleSheet
            var setResult = await controller.SetDataSource(new SetBudgetDataSourceRequest
            { Source = BudgetDataSource.GoogleSheet });
            var okSetResult = Assert.IsType<OkObjectResult>(setResult);
            var dtoSet = Assert.IsType<BudgetDataSourceDto>(okSetResult.Value);
            Assert.Equal(BudgetDataSource.GoogleSheet, dtoSet.Source);
            Assert.False(dtoSet.IsDefault);

            // Get again
            var getResult2 = await controller.GetDataSource();
            var okResult2 = Assert.IsType<OkObjectResult>(getResult2);
            var dto2 = Assert.IsType<BudgetDataSourceDto>(okResult2.Value);
            Assert.Equal(BudgetDataSource.GoogleSheet, dto2.Source);
            Assert.False(dto2.IsDefault);
        }
    }
}
