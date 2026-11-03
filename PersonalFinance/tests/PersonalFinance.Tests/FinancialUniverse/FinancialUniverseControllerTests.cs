using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.FinancialUniverse;
using PersonalFinance.ApiService.Features.FinancialUniverse.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// The Financial Universe endpoint: signed-in only, the current month by default, a keyed 400 for a month outside the
/// range, and the Account's own id handed to the projection.
/// </summary>
public class FinancialUniverseControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Controller_RequiresASignedInAccount()
    {
        Assert.NotNull(typeof(FinancialUniverseController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.DoesNotContain(typeof(FinancialUniverseController).GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }

    [Fact]
    public async Task GetGraph_WithNoMonth_BuildsTheCurrentMonthForTheSignedInAccount()
    {
        var service = new RecordingService();
        FinancialUniverseController controller = CreateController(service, "account-7");

        IActionResult result = await controller.GetGraph(null, null);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(service.Graph, ok.Value);
        Assert.Equal(("account-7", 2026, 10), service.Asked);
    }

    [Fact]
    public async Task GetGraph_WithAMonth_BuildsThatMonth()
    {
        var service = new RecordingService();
        FinancialUniverseController controller = CreateController(service, "account-7");

        await controller.GetGraph(2025, 3);

        Assert.Equal(("account-7", 2025, 3), service.Asked);
    }

    [Theory]
    [InlineData(2026, 13)]
    [InlineData(2026, 0)]
    [InlineData(1999, 6)]
    [InlineData(2101, 6)]
    public async Task GetGraph_OutsideTheRange_AnswersAKeyed400WithoutBuilding(int year, int month)
    {
        var service = new RecordingService();
        FinancialUniverseController controller = CreateController(service, "account-7");

        IActionResult result = await controller.GetGraph(year, month);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(result);
        ApiErrorEntryDto error = Assert.Single(Assert.IsType<ApiErrorDto>(bad.Value).Errors);
        Assert.Equal(ApiErrorKeys.FinancialUniverse.MonthRange, error.Key);
        Assert.Equal(["maxYear", "minYear"], error.Args!.Keys.Order(StringComparer.Ordinal));
        Assert.Null(service.Asked);
    }

    [Fact]
    public void Validator_FillsAMissingPartFromNow()
    {
        Assert.Equal((2026, 10), FinancialUniverseQueryValidator.Resolve(null, null, Now, out ApiErrorEntryDto? none));
        Assert.Null(none);
        Assert.Equal((2024, 10), FinancialUniverseQueryValidator.Resolve(2024, null, Now, out _));
        Assert.Equal((2026, 2), FinancialUniverseQueryValidator.Resolve(null, 2, Now, out _));
        Assert.Equal((FinancialUniverseQueryValidator.MinYear, 1),
            FinancialUniverseQueryValidator.Resolve(FinancialUniverseQueryValidator.MinYear, 1, Now, out _));
        Assert.Equal((FinancialUniverseQueryValidator.MaxYear, 12),
            FinancialUniverseQueryValidator.Resolve(FinancialUniverseQueryValidator.MaxYear, 12, Now, out _));
    }

    [Fact]
    public void Validator_NamesTheFieldThatFailed()
    {
        Assert.Null(FinancialUniverseQueryValidator.Resolve(2026, 13, Now, out ApiErrorEntryDto? month));
        Assert.Equal("month", month!.Field);
        Assert.Null(FinancialUniverseQueryValidator.Resolve(1900, 6, Now, out ApiErrorEntryDto? year));
        Assert.Equal("year", year!.Field);
    }

    private static FinancialUniverseController CreateController(IFinancialUniverseService service, string userId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuthType");
        return new FinancialUniverseController(service, new FixedTimeProvider(Now))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private sealed class RecordingService : IFinancialUniverseService
    {
        public FinancialUniverseGraphDto Graph { get; } = new();
        public (string UserId, int Year, int Month)? Asked { get; private set; }

        public Task<FinancialUniverseGraphDto> GetGraphAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
        {
            Asked = (userId, year, month);
            return Task.FromResult(Graph);
        }
    }
}
