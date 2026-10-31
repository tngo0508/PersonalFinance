using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.InvestmentScenarios;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.InvestmentScenarios;

/// <summary>
/// The Investment Scenarios service against an in-memory database: an Account saves, lists, loads, updates,
/// duplicates and soft-deletes its own scenarios, never another Account's, keeps at most 20, and its list reads last
/// edited first.
/// </summary>
public sealed class InvestmentScenarioServiceTests : IDisposable
{
    private const string UserId = "planner";
    private const string OtherUserId = "someone-else";

    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"InvestmentScenarios_{Guid.NewGuid():N}")
        .Options);

    public void Dispose() => _db.Dispose();

    private InvestmentScenarioService Service() => new(_db, _clock);

    private static SaveInvestmentScenarioRequestDto Request(string name = "Retire at 60") => new()
    {
        Name = name,
        Note = "Base case",
        Mode = InvestmentScenarioMode.Goal,
        CurrencyCode = "VND",
        InitialAmount = 250_000_000m,
        MonthlyContribution = 12_500_000m,
        AssumedReturn = 7.25m,
        Years = 25,
        ContributionIncrease = 2m,
        Inflation = 2.5m,
        Fee = 0.1m,
        DelayYears = 2,
        StopAfterYear = 20,
        GoalTarget = 12_500_000_000m,
        GoalSolveFor = InvestmentGoalSolveFor.Years
    };

    [Fact]
    public async Task Create_SavesEveryInput_AndReturnsTheScenario()
    {
        InvestmentScenarioDto saved = await Service().CreateAsync(UserId, Request("  Retire at 60  "));

        InvestmentScenarioDto loaded = (await Service().GetAsync(UserId, saved.Id))!;
        Assert.Equal("Retire at 60", loaded.Name);
        Assert.Equal("Base case", loaded.Note);
        Assert.Equal(InvestmentScenarioMode.Goal, loaded.Mode);
        Assert.Equal("VND", loaded.CurrencyCode);
        Assert.Equal((250_000_000m, 12_500_000m), (loaded.InitialAmount, loaded.MonthlyContribution));
        Assert.Equal((7.25m, 25, 2m, 2.5m, 0.1m), (loaded.AssumedReturn, loaded.Years, loaded.ContributionIncrease,
            loaded.Inflation, loaded.Fee));
        Assert.Equal((2, (int?)20), (loaded.DelayYears, loaded.StopAfterYear));
        Assert.Equal((12_500_000_000m, InvestmentGoalSolveFor.Years), (loaded.GoalTarget, loaded.GoalSolveFor));
        Assert.Equal(_clock.Now.UtcDateTime, loaded.CreatedAtUtc);
        Assert.Equal(_clock.Now.UtcDateTime, loaded.LastEditedAtUtc);
        Assert.Equal(saved.Id, loaded.Id);
    }

    [Fact]
    public async Task Create_WithoutACurrency_IsInUsDollars_AndAnEmptyNoteIsNone()
    {
        SaveInvestmentScenarioRequestDto request = Request();
        request.CurrencyCode = null;
        request.Note = "   ";

        InvestmentScenarioDto saved = await Service().CreateAsync(UserId, request);

        Assert.Equal(("USD", (string?)null), (saved.CurrencyCode, saved.Note));
    }

    [Fact]
    public async Task Create_AGrowScenario_KeepsNoGoal()
    {
        SaveInvestmentScenarioRequestDto request = Request();
        request.Mode = InvestmentScenarioMode.Grow;

        InvestmentScenarioDto saved = await Service().CreateAsync(UserId, request);

        Assert.Equal((null, null), (saved.GoalTarget, saved.GoalSolveFor));
    }

    [Fact]
    public async Task Create_WhenTheAccountKeepsTwenty_IsRejectedWithTheLimitKey()
    {
        for (int i = 0; i < InvestmentCalculatorLimits.MaxScenariosPerUser; i++)
        {
            await Service().CreateAsync(UserId, Request($"Plan {i}"));
        }

        ApiErrorException rejection = await ApiErrorAssert.ThrowsAsync(ApiErrorKeys.InvestmentScenarios.LimitReached,
            () => Service().CreateAsync(UserId, Request("One too many")));

        Assert.Equal(409, rejection.StatusCode);
        Assert.Equal(InvestmentCalculatorLimits.MaxScenariosPerUser,
            await _db.InvestmentScenarios.CountAsync(s => s.UserId == UserId));
    }

    [Fact]
    public async Task Create_DeletedScenariosAndOtherAccounts_DoNotCountTowardsTheLimit()
    {
        for (int i = 0; i < InvestmentCalculatorLimits.MaxScenariosPerUser; i++)
        {
            await Service().CreateAsync(OtherUserId, Request($"Theirs {i}"));
        }

        for (int i = 0; i < InvestmentCalculatorLimits.MaxScenariosPerUser; i++)
        {
            await Service().CreateAsync(UserId, Request($"Plan {i}"));
        }

        InvestmentScenarioDto first = (await Service().ListAsync(UserId))[0];
        Assert.True(await Service().DeleteAsync(UserId, first.Id));

        InvestmentScenarioDto saved = await Service().CreateAsync(UserId, Request("Room again"));

        Assert.Equal("Room again", saved.Name);
    }

    [Fact]
    public async Task List_ReadsLastEditedFirst_AndBreaksTiesByNewestId()
    {
        InvestmentScenarioDto oldest = await Service().CreateAsync(UserId, Request("Oldest"));
        _clock.Advance(TimeSpan.FromHours(1));
        InvestmentScenarioDto middle = await Service().CreateAsync(UserId, Request("Middle"));
        InvestmentScenarioDto sameTime = await Service().CreateAsync(UserId, Request("Same time"));
        _clock.Advance(TimeSpan.FromHours(1));
        await Service().UpdateAsync(UserId, oldest.Id, Request("Oldest, edited"));

        List<InvestmentScenarioDto> list = await Service().ListAsync(UserId);

        Assert.Equal(["Oldest, edited", "Same time", "Middle"], list.Select(s => s.Name));
        Assert.True(sameTime.Id > middle.Id);
    }

    [Fact]
    public async Task List_LeavesOutDeletedScenariosAndOtherAccounts()
    {
        await Service().CreateAsync(OtherUserId, Request("Theirs"));
        InvestmentScenarioDto deleted = await Service().CreateAsync(UserId, Request("Deleted"));
        await Service().CreateAsync(UserId, Request("Mine"));
        await Service().DeleteAsync(UserId, deleted.Id);

        List<InvestmentScenarioDto> list = await Service().ListAsync(UserId);

        Assert.Equal(["Mine"], list.Select(s => s.Name));
    }

    [Fact]
    public async Task Update_ReplacesTheInputs_KeepsTheCreationDate_AndDatesTheEdit()
    {
        InvestmentScenarioDto saved = await Service().CreateAsync(UserId, Request());
        _clock.Advance(TimeSpan.FromDays(2));
        SaveInvestmentScenarioRequestDto edit = Request("Retire at 55");
        edit.CurrencyCode = null;
        edit.Mode = InvestmentScenarioMode.Grow;
        edit.MonthlyContribution = 20_000_000m;
        edit.StopAfterYear = null;

        InvestmentScenarioDto updated = (await Service().UpdateAsync(UserId, saved.Id, edit))!;

        Assert.Equal(("Retire at 55", 20_000_000m, (int?)null), (updated.Name, updated.MonthlyContribution, updated.StopAfterYear));
        Assert.Equal((null, null), (updated.GoalTarget, updated.GoalSolveFor));
        Assert.Equal("VND", updated.CurrencyCode);
        Assert.Equal(saved.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal(_clock.Now.UtcDateTime, updated.LastEditedAtUtc);
    }

    [Fact]
    public async Task Duplicate_CopiesEveryInputUnderTheNewName_AsANewScenario()
    {
        InvestmentScenarioDto saved = await Service().CreateAsync(UserId, Request());
        _clock.Advance(TimeSpan.FromHours(3));

        InvestmentScenarioDto copy = (await Service().DuplicateAsync(UserId, saved.Id, " Copy of Retire at 60 "))!;

        Assert.NotEqual(saved.Id, copy.Id);
        Assert.Equal("Copy of Retire at 60", copy.Name);
        Assert.Equal(_clock.Now.UtcDateTime, copy.CreatedAtUtc);
        Assert.Equal((saved.Note, saved.Mode, saved.CurrencyCode, saved.InitialAmount, saved.MonthlyContribution,
                saved.AssumedReturn, saved.Years, saved.ContributionIncrease, saved.Inflation, saved.Fee,
                saved.DelayYears, saved.StopAfterYear, saved.GoalTarget, saved.GoalSolveFor),
            (copy.Note, copy.Mode, copy.CurrencyCode, copy.InitialAmount, copy.MonthlyContribution,
                copy.AssumedReturn, copy.Years, copy.ContributionIncrease, copy.Inflation, copy.Fee,
                copy.DelayYears, copy.StopAfterYear, copy.GoalTarget, copy.GoalSolveFor));
        Assert.Equal(2, (await Service().ListAsync(UserId)).Count);
    }

    [Fact]
    public async Task Duplicate_WhenTheAccountKeepsTwenty_IsRejectedWithTheLimitKey()
    {
        InvestmentScenarioDto first = await Service().CreateAsync(UserId, Request("Plan 0"));
        for (int i = 1; i < InvestmentCalculatorLimits.MaxScenariosPerUser; i++)
        {
            await Service().CreateAsync(UserId, Request($"Plan {i}"));
        }

        await ApiErrorAssert.ThrowsAsync(ApiErrorKeys.InvestmentScenarios.LimitReached,
            () => Service().DuplicateAsync(UserId, first.Id, "Copy"));
    }

    [Fact]
    public async Task Delete_IsSoft_AndTheScenarioIsGoneFromReads()
    {
        InvestmentScenarioDto saved = await Service().CreateAsync(UserId, Request());
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.True(await Service().DeleteAsync(UserId, saved.Id));

        Assert.Null(await Service().GetAsync(UserId, saved.Id));
        InvestmentScenario row = await _db.InvestmentScenarios.IgnoreQueryFilters().SingleAsync(s => s.Id == saved.Id);
        Assert.True(row.IsDeleted);
        Assert.Equal(_clock.Now.UtcDateTime, row.DeletedAt);
        Assert.False(await Service().DeleteAsync(UserId, saved.Id));
    }

    [Fact]
    public async Task AnotherAccountsScenario_CannotBeRead_Updated_Duplicated_OrDeleted()
    {
        InvestmentScenarioDto theirs = await Service().CreateAsync(OtherUserId, Request("Theirs"));

        Assert.Null(await Service().GetAsync(UserId, theirs.Id));
        Assert.Null(await Service().UpdateAsync(UserId, theirs.Id, Request("Hijacked")));
        Assert.Null(await Service().DuplicateAsync(UserId, theirs.Id, "Stolen copy"));
        Assert.False(await Service().DeleteAsync(UserId, theirs.Id));

        InvestmentScenarioDto untouched = (await Service().GetAsync(OtherUserId, theirs.Id))!;
        Assert.Equal("Theirs", untouched.Name);
        Assert.Empty(await Service().ListAsync(UserId));
    }

    [Fact]
    public void ListQuery_TranslatesToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        AzureSqlTranslation.AssertTranslates(new InvestmentScenarioService(db, _clock).ListQuery(UserId));
    }
}
