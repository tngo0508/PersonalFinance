using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;

namespace PersonalFinance.Tests.SavingsGoals;

/// <summary>
/// The goal target date comes from <c>&lt;input type="date"&gt;</c>, which always posts ISO <c>yyyy-MM-dd</c>.
/// </summary>
public class SavingsGoalsControllerTests
{
    [Fact]
    public async Task Create_ParsesIsoTargetDate()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer));

        IActionResult result = await controller.Create("Emergency fund", 1000m, 0m, null, "2026-02-03");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(new DateTime(2026, 2, 3), api.Created?.TargetDate);
    }

    [Fact]
    public async Task Create_WithoutTargetDate_SendsNoTargetDate()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer));

        await controller.Create("Emergency fund", 1000m, 0m, null, "");

        Assert.NotNull(api.Created);
        Assert.Null(api.Created.TargetDate);
    }

    [Theory]
    [InlineData("02/03/2026")]
    [InlineData("not-a-date")]
    public async Task Create_WithMalformedTargetDate_ThrowsWithoutCallingApi(string targetDate)
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer));

        await Assert.ThrowsAsync<FormatException>(() =>
            controller.Create("Emergency fund", 1000m, 0m, null, targetDate));

        Assert.Null(api.Created);
    }

    [Fact]
    public async Task Create_WhenAnAmountDidNotBind_DoesNotCallTheApi()
    {
        // "1234.56" posted under vi-VN is rejected by the binder: the action sees 0 and a ModelState error.
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer));
        controller.ModelState.AddModelError("currentAmount", "The value '1234.56' is not valid.");

        await controller.Create("Emergency fund", 1000m, 0m, null, "");

        Assert.Null(api.Created);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Update_WhenTheAmountDidNotBind_KeepsTheGoal_AndSaysWhy()
    {
        // Without this check a rejected amount would arrive as 0 and wipe the goal's saved amount.
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer));
        controller.ModelState.AddModelError("currentAmount", "The value '1234.56' is not valid.");

        IActionResult result = await controller.Update(7, 0m, false);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(api.Updated);
        Assert.Equal("The value '1234.56' is not valid.", controller.TempData["Error"]);
    }

    private static SavingsGoalsController WithContext(SavingsGoalsController controller)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-123") }, "TestAuthType"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }

    private sealed class RecordingSavingsGoalsApi : ISavingsGoalsApi
    {
        public CreateSavingsGoalRequestDto? Created { get; private set; }

        public Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<SavingsGoalDto>());

        public Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync(CreateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Created = request;
            return Task.FromResult(new CreateSavingsGoalResponseDto());
        }

        public UpdateSavingsGoalRequestDto? Updated { get; private set; }

        public Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, UpdateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Updated = request;
            return Task.FromResult(new UpdateSavingsGoalResponseDto());
        }

        public Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
