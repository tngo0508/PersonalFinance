using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using Refit;

namespace PersonalFinance.Tests.SavingsGoals;

/// <summary>
/// The goal target date comes from <c>&lt;input type="date"&gt;</c>, which always posts ISO <c>yyyy-MM-dd</c>.
/// Contribution forms post a positive amount and a direction; rejections are explained in the page language.
/// </summary>
public class SavingsGoalsControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_ParsesIsoTargetDate()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

        IActionResult result = await controller.Create("Emergency fund", 1000m, 0m, null, "2026-02-03");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(new DateTime(2026, 2, 3), api.Created?.TargetDate);
    }

    [Fact]
    public async Task Create_WithoutTargetDate_SendsNoTargetDate()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

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
        SavingsGoalsController controller = Controller(api);

        await Assert.ThrowsAsync<FormatException>(() =>
            controller.Create("Emergency fund", 1000m, 0m, null, targetDate));

        Assert.Null(api.Created);
    }

    [Fact]
    public async Task Create_WhenAnAmountDidNotBind_DoesNotCallTheApi()
    {
        // "1234.56" posted under vi-VN is rejected by the binder: the action sees 0 and a ModelState error.
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);
        controller.ModelState.AddModelError("currentAmount", "The value '1234.56' is not valid.");

        await controller.Create("Emergency fund", 1000m, 0m, null, "");

        Assert.Null(api.Created);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task AddContribution_Withdrawal_SendsANegativeAmount_DatedAsPosted()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

        await controller.AddContribution(7, 40m, "withdrawal", "2026-10-20", "  Car repair  ");

        Assert.Equal(7, api.ContributionGoalId);
        Assert.Equal(-40m, api.Contribution?.Amount);
        Assert.Equal(new DateOnly(2026, 10, 20), api.Contribution?.Date);
        Assert.Equal("Car repair", api.Contribution?.Note);
        Assert.Equal("Contribution added.", controller.TempData["Success"]);
    }

    [Fact]
    public async Task AddContribution_WithoutADate_LeavesItToTheApi_AsToday()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

        await controller.AddContribution(7, 40m, "deposit", "", null);

        Assert.Equal(40m, api.Contribution?.Amount);
        Assert.Null(api.Contribution?.Date);
        Assert.Null(api.Contribution?.Note);
    }

    [Theory]
    [InlineData(0, null, "Enter an amount greater than zero.")]
    [InlineData(10, "2026-10-26", "A Contribution can't be dated in the future.")]
    [InlineData(10, "1999-12-31", "A Contribution can't be dated before the year 2000.")]
    [InlineData(10, "26/10/2026", "Enter a valid date.")]
    public async Task AddContribution_Rejected_SaysWhy_WithoutCallingTheApi(int amount, string? date, string message)
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

        IActionResult result = await controller.AddContribution(7, amount, "deposit", date, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(api.Contribution);
        Assert.Equal(message, controller.TempData["Error"]);
    }

    [Fact]
    public async Task AddContribution_WhenTheAmountDidNotBind_ShowsTheBindersMessage()
    {
        // Without this check a rejected amount would arrive as 0 and read as "amount required".
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);
        controller.ModelState.AddModelError("amount", "The value '1234.56' is not valid.");

        await controller.AddContribution(7, 0m, "deposit", null, null);

        Assert.Null(api.Contribution);
        Assert.Equal("The value '1234.56' is not valid.", controller.TempData["Error"]);
    }

    [Fact]
    public async Task AddContribution_WithANoteOver200Characters_IsRejected()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

        await controller.AddContribution(7, 10m, "deposit", null, new string('n', 201));

        Assert.Null(api.Contribution);
        Assert.Equal("Keep the note to 200 characters or fewer.", controller.TempData["Error"]);
    }

    [Fact]
    public async Task AddContribution_WhenTheApiSaysBelowZero_SaysSoInThePageLanguage()
    {
        var api = new RecordingSavingsGoalsApi { Failure = HttpStatusCode.Conflict };
        SavingsGoalsController controller = Controller(api);

        await controller.AddContribution(7, 500m, "withdrawal", null, null);

        Assert.Equal("That would take the goal's saved amount below zero.", controller.TempData["Error"]);
    }

    [Fact]
    public async Task EditContribution_SendsTheEditedContribution()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

        await controller.EditContribution(7, 3, 25m, "deposit", "2026-10-01", "Bonus");

        Assert.Equal((7, 3), (api.ContributionGoalId, api.ContributionId));
        Assert.Equal(25m, api.Contribution?.Amount);
        Assert.Equal("Contribution updated.", controller.TempData["Success"]);
    }

    [Fact]
    public async Task DeleteContribution_DeletesIt_AndConfirms()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);

        await controller.DeleteContribution(7, 3);

        Assert.Equal((7, 3), (api.ContributionGoalId, api.ContributionId));
        Assert.Equal("Contribution deleted.", controller.TempData["Success"]);
    }

    [Theory]
    [InlineData(5, "Balance corrected.")]
    [InlineData(null, "The saved amount already matches that figure.")]
    public async Task CorrectBalance_SaysWhetherAnAdjustmentWasRecorded(int? adjustmentId, string message)
    {
        var api = new RecordingSavingsGoalsApi { SavedId = adjustmentId };
        SavingsGoalsController controller = Controller(api);

        await controller.CorrectBalance(7, 1200m);

        Assert.Equal(1200m, api.Balance?.Balance);
        Assert.Equal(message, controller.TempData["Success"]);
    }

    [Fact]
    public async Task CorrectBalance_WhenTheAmountDidNotBind_KeepsTheGoal_AndSaysWhy()
    {
        // Without this check a rejected amount would arrive as 0 and empty the goal.
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = Controller(api);
        controller.ModelState.AddModelError("balance", "The value '1234.56' is not valid.");

        IActionResult result = await controller.CorrectBalance(7, 0m);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(api.Balance);
        Assert.Equal("The value '1234.56' is not valid.", controller.TempData["Error"]);
    }

    private static SavingsGoalsController Controller(ISavingsGoalsApi api)
    {
        var controller = new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance,
            TestLocalization.Localizer, new FixedTimeProvider(Now));
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
        public SaveContributionRequestDto? Contribution { get; private set; }
        public CorrectBalanceRequestDto? Balance { get; private set; }
        public int? ContributionGoalId { get; private set; }
        public int? ContributionId { get; private set; }
        public int? SavedId { get; init; } = 1;
        public HttpStatusCode? Failure { get; init; }

        public Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<SavingsGoalDto>());

        public Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync(CreateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Created = request;
            return Task.FromResult(new CreateSavingsGoalResponseDto());
        }

        public Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, UpdateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<List<ContributionDto>> GetContributionsAsync(int goalId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<SaveContributionResponseDto> AddContributionAsync(int goalId,
            SaveContributionRequestDto request, CancellationToken cancellationToken = default)
        {
            await ThrowIfFailingAsync();
            (ContributionGoalId, Contribution) = (goalId, request);
            return new SaveContributionResponseDto { Success = true, Id = SavedId };
        }

        public async Task<SaveContributionResponseDto> UpdateContributionAsync(int goalId, int contributionId,
            SaveContributionRequestDto request, CancellationToken cancellationToken = default)
        {
            await ThrowIfFailingAsync();
            (ContributionGoalId, ContributionId, Contribution) = (goalId, contributionId, request);
            return new SaveContributionResponseDto { Success = true, Id = contributionId };
        }

        public async Task<DeleteContributionResponseDto> DeleteContributionAsync(int goalId, int contributionId,
            CancellationToken cancellationToken = default)
        {
            await ThrowIfFailingAsync();
            (ContributionGoalId, ContributionId) = (goalId, contributionId);
            return new DeleteContributionResponseDto { Success = true };
        }

        public async Task<SaveContributionResponseDto> CorrectBalanceAsync(int goalId, CorrectBalanceRequestDto request,
            CancellationToken cancellationToken = default)
        {
            await ThrowIfFailingAsync();
            (ContributionGoalId, Balance) = (goalId, request);
            return new SaveContributionResponseDto { Success = true, Id = SavedId };
        }

        private async Task ThrowIfFailingAsync()
        {
            if (Failure is { } status)
            {
                throw await ApiException.Create(new HttpRequestMessage(HttpMethod.Post, "http://api/"),
                    HttpMethod.Post, new HttpResponseMessage(status), new RefitSettings());
            }
        }
    }
}
