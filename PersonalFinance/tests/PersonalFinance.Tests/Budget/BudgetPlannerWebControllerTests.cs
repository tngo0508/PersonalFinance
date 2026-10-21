using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Tests.Budget;

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
            Transportation = 350.00m,
            Utilities = 199.84m,
            Insurance = 175.18m,
            Healthcare = 375.02m,
            Savings = 500.00m,
            DebtRepayment = 300.00m,
            Entertainment = 200.00m,
            Miscellaneous = 76.82m // total = 3520.00 < 3575.00
        };

        var result = await controller.Save(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.NotNull(fakeApi.SavedRequest);
        Assert.Equal(3575.00m, fakeApi.SavedRequest!.MonthlyIncome);
        Assert.Equal(700.00m, fakeApi.SavedRequest.Allocations.Housing);
        Assert.Equal(643.14m, fakeApi.SavedRequest.Allocations.Food);
        Assert.Equal(350.00m, fakeApi.SavedRequest.Allocations.Transportation);
        Assert.Equal(199.84m, fakeApi.SavedRequest.Allocations.Utilities);
        Assert.Equal(175.18m, fakeApi.SavedRequest.Allocations.Insurance);
        Assert.Equal(375.02m, fakeApi.SavedRequest.Allocations.Healthcare);
        Assert.Equal(500.00m, fakeApi.SavedRequest.Allocations.Savings);
        Assert.Equal(300.00m, fakeApi.SavedRequest.Allocations.DebtRepayment);
        Assert.Equal(200.00m, fakeApi.SavedRequest.Allocations.Entertainment);
        Assert.Equal(76.82m, fakeApi.SavedRequest.Allocations.Miscellaneous);
        Assert.Equal("Budget saved successfully.", controller.TempData["SaveSuccess"]);
    }

    [Fact]
    public async Task Index_WhenBudgetLoadedFromApi_PopulatesAllCategoriesIncludingTransportation()
    {
        var fakeApi = new FakePersonalFinanceApi
        {
            BudgetResponse = new BudgetResponseDto
            {
                MonthlyIncome = 4000.00m,
                Allocations = new BudgetAllocationsDto
                {
                    Housing = 1000m,
                    Food = 500m,
                    Transportation = 300m,
                    Utilities = 200m,
                    Insurance = 150m,
                    Healthcare = 100m,
                    Savings = 800m,
                    DebtRepayment = 400m,
                    Entertainment = 250m,
                    Miscellaneous = 300m
                }
            }
        };

        var controller = CreateController(fakeApi);
        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BudgetViewModel>(viewResult.Model);
        Assert.Equal(4000.00m, model.MonthlyIncome);
        Assert.Equal(1000m, model.Housing);
        Assert.Equal(500m, model.Food);
        Assert.Equal(300m, model.Transportation);
        Assert.Equal(200m, model.Utilities);
        Assert.Equal(150m, model.Insurance);
        Assert.Equal(100m, model.Healthcare);
        Assert.Equal(800m, model.Savings);
        Assert.Equal(400m, model.DebtRepayment);
        Assert.Equal(250m, model.Entertainment);
        Assert.Equal(300m, model.Miscellaneous);
    }

    [Fact]
    public async Task Save_WhenAllocationsExceedMonthlyIncome_SavesBudgetAndSetsWarning()
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

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.NotNull(fakeApi.SavedRequest);
        Assert.Equal(3000.00m, fakeApi.SavedRequest!.MonthlyIncome);
        Assert.Equal(2000.00m, fakeApi.SavedRequest.Allocations.Housing);
        Assert.Equal(1500.00m, fakeApi.SavedRequest.Allocations.Food);
        Assert.NotNull(controller.TempData["SaveWarning"]);
        Assert.Contains("exceed your monthly income", controller.TempData["SaveWarning"]!.ToString());
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

    [Fact]
    public void BudgetIndexView_ContainsAspForBindingsForAllCategories()
    {
        var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/PersonalFinance.Web/Views/Budget/Index.cshtml"));
        if (!File.Exists(viewPath))
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "PersonalFinance.sln")))
            {
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            viewPath = Path.Combine(dir, "PersonalFinance", "src", "PersonalFinance.Web", "Views", "Budget", "Index.cshtml");
        }

        Assert.True(File.Exists(viewPath), $"View file should exist at: {viewPath}");
        var content = File.ReadAllText(viewPath);
        var expectedCategories = new[]
        {
            "Housing", "Food", "Transportation", "Utilities", "Insurance",
            "Healthcare", "Savings", "DebtRepayment", "Entertainment", "Miscellaneous"
        };

        foreach (var category in expectedCategories)
        {
            Assert.Contains($"asp-for=\"{category}\"", content);
        }
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
