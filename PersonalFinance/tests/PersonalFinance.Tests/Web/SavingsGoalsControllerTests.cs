using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Tests.Budget;
using PersonalFinance.Web.Features.SavingsGoals;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// The goal target date comes from <c>&lt;input type="date"&gt;</c>, which always posts ISO <c>yyyy-MM-dd</c>.
/// </summary>
public class SavingsGoalsControllerTests
{
    [Fact]
    public async Task Create_ParsesIsoTargetDate()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance));

        IActionResult result = await controller.Create("Emergency fund", 1000m, 0m, null, "2026-02-03");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(new DateTime(2026, 2, 3), api.Created?.TargetDate);
    }

    [Fact]
    public async Task Create_WithoutTargetDate_SendsNoTargetDate()
    {
        var api = new RecordingSavingsGoalsApi();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance));

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
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance));

        await Assert.ThrowsAsync<FormatException>(() =>
            controller.Create("Emergency fund", 1000m, 0m, null, targetDate));

        Assert.Null(api.Created);
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

        public Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, UpdateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
