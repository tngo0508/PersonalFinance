using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.BudgetTracker;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// The Budget Tracker's own messages answer in the request's Language: the page's load error and the AJAX endpoints'
/// rejected-request errors, which the page shows in its alert, come from the localizer, never hardcoded English.
/// </summary>
public class BudgetTrackerControllerLocalizationTests
{
    [Fact]
    public async Task Index_WhenTheApiFails_ShowsTheLoadErrorInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi
        {
            Failure = new HttpRequestException("down")
        });

        IActionResult result = await controller.Index(2026, 10);

        BudgetTrackerViewModel model = Assert.IsType<BudgetTrackerViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(TestLocalization.Vietnamese("BudgetTracker.Message.LoadFailed"), model.ErrorMessage);
    }

    [Fact]
    public async Task Index_WhenTheApiFails_ShowsTheLoadErrorInEnglish()
    {
        using var culture = new UiCultureScope("en");
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi
        {
            Failure = new HttpRequestException("down")
        });

        IActionResult result = await controller.Index(2026, 10);

        BudgetTrackerViewModel model = Assert.IsType<BudgetTrackerViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Unable to load budget tracker data. Please try again.", model.ErrorMessage);
    }

    public static TheoryData<string, Func<BudgetTrackerController, Task<IActionResult>>> RejectedRequests() => new()
    {
        { "BudgetTracker.Message.InvalidInitializeMonth", c => c.InitializeMonth(null) },
        { "BudgetTracker.Message.InvalidPlanned", c => c.SetPlanned(2026, 10, 1, null) },
        { "BudgetTracker.Message.InvalidActualOverride", c => c.SetActualOverride(2026, 10, 1, null) },
        { "BudgetTracker.Message.InvalidCategory", c => c.CreateCategory(null) },
        { "BudgetTracker.Message.InvalidCategoryUpdate", c => c.UpdateCategory(1, null) },
        { "BudgetTracker.Message.InvalidTransaction", c => c.CreateTransaction(null) },
        { "BudgetTracker.Message.InvalidTransactionUpdate", c => c.UpdateTransaction(1, null) },
        { "BudgetTracker.Message.InvalidDataSource", c => c.SetDataSource(null) }
    };

    [Theory]
    [MemberData(nameof(RejectedRequests))]
    public async Task RejectedRequest_ReturnsTheErrorInTheRequestLanguage(string key,
        Func<BudgetTrackerController, Task<IActionResult>> action)
    {
        using var culture = new UiCultureScope("vi");
        BudgetTrackerController controller = CreateController(new FakeBudgetTrackerApi());

        IActionResult result = await action(controller);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal(TestLocalization.Vietnamese(key), json.RootElement.GetProperty("error").GetString());
    }

    private static BudgetTrackerController CreateController(FakeBudgetTrackerApi api) =>
        new(api, new RecordingLogger<BudgetTrackerController>(), TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "tracker-user") }, "TestAuthType"))
                }
            }
        };
}
