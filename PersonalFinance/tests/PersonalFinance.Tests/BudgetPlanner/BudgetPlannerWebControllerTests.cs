using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Budget;

namespace PersonalFinance.Tests.BudgetPlanner;

/// <summary>
/// Unit tests for <see cref="BudgetController"/>.
/// </summary>
public class BudgetPlannerWebControllerTests
{
    private static BudgetController CreateController(IBudgetPlannerApi api)
    {
        var identity = new ClaimsIdentity(new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-123"),
            new(ClaimTypes.Name, "test-user-123@example.com")
        }, "TestAuthType");

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());

        return new BudgetController(api, NullLogger<BudgetController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
    }

    [Fact]
    public async Task Save_WhenAllocationsAreLessThanMonthlyIncome_SavesSuccessfully()
    {
        var fakeApi = new FakeBudgetPlannerApi();
        BudgetController controller = CreateController(fakeApi);
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

        IActionResult result = await controller.Save(model);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
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
    public async Task Save_InVietnamese_ConfirmsInVietnamese()
    {
        using var culture = new UiCultureScope("vi");
        BudgetController controller = CreateController(new FakeBudgetPlannerApi());

        await controller.Save(new BudgetViewModel { MonthlyIncome = 3000m, Housing = 1000m });

        Assert.Equal(TestLocalization.Vietnamese("BudgetPlanner.Message.Saved"), controller.TempData["SaveSuccess"]);
    }

    [Fact]
    public async Task Save_OverIncome_InVietnamese_WarnsInVietnamese_WithBothAmounts()
    {
        using var culture = new UiCultureScope("vi");
        BudgetController controller = CreateController(new FakeBudgetPlannerApi());

        await controller.Save(new BudgetViewModel { MonthlyIncome = 3000m, Housing = 2000m, Food = 1500m });

        string expected = TestLocalization.Vietnamese("BudgetPlanner.Message.SavedOverIncome")
            .Replace("{total}", MoneyFormatter.Format(3500m, "USD"), StringComparison.Ordinal)
            .Replace("{income}", MoneyFormatter.Format(3000m, "USD"), StringComparison.Ordinal);
        Assert.Equal(expected, controller.TempData["SaveWarning"]);
    }

    [Fact]
    public async Task Save_WithoutIncome_InVietnamese_SaysWhyInVietnamese()
    {
        using var culture = new UiCultureScope("vi");
        BudgetController controller = CreateController(new FakeBudgetPlannerApi());

        await controller.Save(new BudgetViewModel { MonthlyIncome = 0m });

        Assert.Equal(TestLocalization.Vietnamese("BudgetPlanner.Message.IncomeRequired"),
            Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Save_WhenTheApiFails_InVietnamese_SaysSoInVietnamese()
    {
        using var culture = new UiCultureScope("vi");
        var fakeApi = new FakeBudgetPlannerApi { SaveException = new InvalidOperationException("boom") };
        BudgetController controller = CreateController(fakeApi);

        IActionResult result = await controller.Save(new BudgetViewModel { MonthlyIncome = 3000m });

        Assert.IsType<ViewResult>(result);
        Assert.Equal(TestLocalization.Vietnamese("BudgetPlanner.Message.SaveFailed"),
            Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Index_WhenTheApiFails_InVietnamese_ShowsTheUnavailableStateInVietnamese()
    {
        using var culture = new UiCultureScope("vi");
        BudgetController controller = CreateController(new FakeBudgetPlannerApi { GetException = new HttpRequestException() });

        IActionResult result = await controller.Index();

        UnavailableStateViewModel model = Assert.IsType<UnavailableStateViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(TestLocalization.Vietnamese("BudgetPlanner.Title"), model.PageTitle);
        Assert.Equal(TestLocalization.Vietnamese("BudgetPlanner.Message.Unavailable"), model.Heading);
    }

    [Fact]
    public async Task Index_WhenBudgetLoadedFromApi_PopulatesAllCategoriesIncludingTransportation()
    {
        var fakeApi = new FakeBudgetPlannerApi
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

        BudgetController controller = CreateController(fakeApi);
        IActionResult result = await controller.Index();

        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        BudgetViewModel model = Assert.IsType<BudgetViewModel>(viewResult.Model);
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
        var fakeApi = new FakeBudgetPlannerApi();
        BudgetController controller = CreateController(fakeApi);
        var model = new BudgetViewModel
        {
            MonthlyIncome = 3000.00m,
            Housing = 2000.00m,
            Food = 1500.00m // total = 3500.00 > 3000.00
        };

        IActionResult result = await controller.Save(model);

        RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
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
        var fakeApi = new FakeBudgetPlannerApi();
        BudgetController controller = CreateController(fakeApi);
        var model = new BudgetViewModel
        {
            MonthlyIncome = 0m
        };

        IActionResult result = await controller.Save(model);

        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        Assert.Null(fakeApi.SavedRequest);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public void BudgetIndexView_ContainsAspForBindingsForAllCategories()
    {
        string viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/PersonalFinance.Web/Views/Budget/Index.cshtml"));
        if (!File.Exists(viewPath))
        {
            string dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "PersonalFinance.sln")))
            {
                DirectoryInfo? parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            viewPath = Path.Combine(dir, "PersonalFinance", "src", "PersonalFinance.Web", "Views", "Budget", "Index.cshtml");
        }

        Assert.True(File.Exists(viewPath), $"View file should exist at: {viewPath}");
        string content = File.ReadAllText(viewPath);
        string[] expectedCategories = new[]
        {
            "Housing", "Food", "Transportation", "Utilities", "Insurance",
            "Healthcare", "Savings", "DebtRepayment", "Entertainment", "Miscellaneous"
        };

        foreach (string? category in expectedCategories)
        {
            Assert.Contains($"asp-for=\"{category}\"", content);
        }
    }
}

public class FakeBudgetPlannerApi : IBudgetPlannerApi
{
    public SaveBudgetRequestDto? SavedRequest { get; private set; }
    public BudgetResponseDto BudgetResponse { get; set; } = new();
    public Exception? GetException { get; set; }
    public Exception? SaveException { get; set; }

    public Task<BudgetResponseDto> GetBudgetAsync(string period, CancellationToken cancellationToken = default)
        => GetException is null ? Task.FromResult(BudgetResponse) : Task.FromException<BudgetResponseDto>(GetException);

    public Task<SaveBudgetResponseDto> SaveBudgetAsync(SaveBudgetRequestDto request, CancellationToken cancellationToken = default)
    {
        if (SaveException is not null)
        {
            return Task.FromException<SaveBudgetResponseDto>(SaveException);
        }

        SavedRequest = request;
        return Task.FromResult(new SaveBudgetResponseDto { Success = true });
    }
}
