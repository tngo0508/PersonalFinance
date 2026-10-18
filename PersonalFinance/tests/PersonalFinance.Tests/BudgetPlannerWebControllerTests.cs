using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Tests;

/// <summary>
/// Unit tests for <see cref="BudgetController"/>.
/// </summary>
public class BudgetPlannerWebControllerTests
{
    private static BudgetController CreateController(IPersonalFinanceApi api)
    {
        var identity = new ClaimsIdentity(new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-123"),
            new(ClaimTypes.Name, "test-user-123@example.com")
        }, "TestAuthType");

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());

        return new BudgetController(api)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
    }

    [Fact]
    public async Task Save_WhenAllocationsAreLessThanMonthlyIncome_SavesSuccessfully()
    {
        var fakeApi = new FakePersonalFinanceApi();
        var controller = CreateController(fakeApi);
        var model = new BudgetViewModel
        {
            MonthlyIncome = 3575.00m,
            Housing = 700.00m,
            Food = 643.14m,
            Transportation = 0.00m,
            Utilities = 199.84m,
            Insurance = 175.18m,
            Healthcare = 375.02m,
            Savings = 500.00m,
            DebtRepayment = 300.00m,
            Entertainment = 200.00m,
            Miscellaneous = 417.49m // total = 3510.67 < 3575.00
        };

        var result = await controller.Save(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.NotNull(fakeApi.SavedRequest);
        Assert.Equal(3575.00m, fakeApi.SavedRequest!.MonthlyIncome);
        Assert.Equal(700.00m, fakeApi.SavedRequest.Allocations.Housing);
        Assert.Equal("Budget saved successfully.", controller.TempData["SaveSuccess"]);
    }

    [Fact]
    public async Task Save_WhenAllocationsExceedMonthlyIncome_ReturnsViewWithError()
    {
        var fakeApi = new FakePersonalFinanceApi();
        var controller = CreateController(fakeApi);
        var model = new BudgetViewModel
        {
            MonthlyIncome = 3000.00m,
            Housing = 2000.00m,
            Food = 1500.00m // total = 3500.00 > 3000.00
        };

        var result = await controller.Save(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        Assert.Null(fakeApi.SavedRequest);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("Budget allocations cannot exceed monthly income"));
    }

    [Fact]
    public async Task Save_WhenMonthlyIncomeIsZeroOrNegative_ReturnsViewWithError()
    {
        var fakeApi = new FakePersonalFinanceApi();
        var controller = CreateController(fakeApi);
        var model = new BudgetViewModel
        {
            MonthlyIncome = 0m
        };

        var result = await controller.Save(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        Assert.Null(fakeApi.SavedRequest);
        Assert.False(controller.ModelState.IsValid);
    }
}

public class FakePersonalFinanceApi : IPersonalFinanceApi
{
    public SaveBudgetRequestDto? SavedRequest { get; private set; }
    public BudgetResponseDto BudgetResponse { get; set; } = new();

    public Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<SavingsGoalDto>());

    public Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync(CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new CreateSavingsGoalResponseDto { Success = true });

    public Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, UpdateSavingsGoalRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new UpdateSavingsGoalResponseDto { Success = true });

    public Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(new DeleteSavingsGoalResponseDto { Success = true });

    public Task<List<SavedArticleDto>> GetWatchlistAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<SavedArticleDto>());

    public Task<SaveArticleResponseDto> SaveArticleAsync(SaveArticleRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SaveArticleResponseDto { Success = true });

    public Task<RemoveArticleResponseDto> RemoveArticleAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(new RemoveArticleResponseDto { Success = true });

    public Task<CheckArticleSavedResponseDto> CheckArticleSavedAsync(string url, CancellationToken cancellationToken = default)
        => Task.FromResult(new CheckArticleSavedResponseDto());

    public Task<BudgetResponseDto> GetBudgetAsync(string period, CancellationToken cancellationToken = default)
        => Task.FromResult(BudgetResponse);

    public Task<SaveBudgetResponseDto> SaveBudgetAsync(SaveBudgetRequestDto request, CancellationToken cancellationToken = default)
    {
        SavedRequest = request;
        return Task.FromResult(new SaveBudgetResponseDto { Success = true });
    }

    public Task<AnalysisDashboardDto> GetAnalysisDashboardAsync(int? year = null, int? month = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new AnalysisDashboardDto());

    public Task<SyncSpreadsheetResponseDto> SyncSpreadsheetAsync(SyncSpreadsheetRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SyncSpreadsheetResponseDto());

    public Task<List<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<int>());

    public Task<List<int>> GetAvailableMonthsAsync(int year, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<int>());
}

public class TestTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object> _data = new();

    public IDictionary<string, object> LoadTempData(HttpContext context) => _data;

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
        _data.Clear();
        foreach (var kvp in values)
        {
            _data[kvp.Key] = kvp.Value;
        }
    }
}
