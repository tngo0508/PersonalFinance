using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.MoneyPriorities;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// <see cref="IMoneyPrioritiesService"/> on the InMemory provider: saving a Priority Walk with only its on-path
/// answers, skipping a submit identical to the latest Walk, the 10-newest list, viewing a Walk with its figures
/// recomputed, soft delete, and Account isolation. Expected figures are the spec's golden values (#315).
/// </summary>
public class MoneyPrioritiesServiceTests
{
    private const string UserId = "walk-user";
    private const string OtherUserId = "other-user";
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 30, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MoneyPriorities_{Guid.NewGuid():N}")
            .Options);

    private static MoneyPrioritiesService CreateService(AppDbContext db, DateTimeOffset? now = null) =>
        MoneyPrioritiesTestServices.Service(db, new FixedTimeProvider(now ?? Now));

    /// <summary>$5,000 at 22% APR, paying $500 a month: L1.</summary>
    private static SubmitPriorityWalkRequest DebtRequest() => new()
    {
        MonthlyAmount = 500m,
        DebtApr = 22m,
        DebtBalance = 5_000m
    };

    /// <summary>No debt, fund covered, full match, the given horizon: L4, L5 or Q5.</summary>
    private static SubmitPriorityWalkRequest HorizonRequest(int years) => new()
    {
        MonthlyAmount = 500m,
        DebtApr = 0m,
        EmergencyFundMonths = 6m,
        HasFullEmployerMatch = true,
        HorizonYears = years,
        AgeBand = years > 10 ? RetirementAgeBand.Age60To63 : null,
        AtContributionLimit = years > 10 ? false : null
    };

    [Fact]
    public async Task Submit_SavesTheWalk_WithOffPathAnswersNull()
    {
        await using AppDbContext db = CreateContext();
        SubmitPriorityWalkRequest request = DebtRequest();
        request.Purpose = "  Be debt free by spring  ";
        request.EmergencyFundMonths = 2m;
        request.MonthlyExpenses = 3_000m;
        request.HasFullEmployerMatch = false;
        request.EmployerMatchPercent = 4m;
        request.HorizonYears = 30;
        request.AgeBand = RetirementAgeBand.Under50;
        request.AtContributionLimit = true;

        PriorityWalkResultDto result = await CreateService(db).SubmitAsync(UserId, request);

        PriorityWalk walk = Assert.Single(await db.PriorityWalks.ToListAsync());
        Assert.Equal(result.WalkId, walk.Id);
        Assert.Equal(UserId, walk.UserId);
        Assert.Equal(500m, walk.MonthlyAmount);
        Assert.Equal("Be debt free by spring", walk.Purpose);
        Assert.Equal(22m, walk.DebtApr);
        Assert.Equal(5_000m, walk.DebtBalance);
        Assert.Null(walk.EmergencyFundMonths);
        Assert.Null(walk.MonthlyExpenses);
        Assert.Null(walk.HasFullEmployerMatch);
        Assert.Null(walk.EmployerMatchPercent);
        Assert.Null(walk.HorizonYears);
        Assert.Null(walk.AgeBand);
        Assert.Null(walk.AtContributionLimit);
        Assert.Equal(1, walk.TreeVersion);
        Assert.Null(walk.LimitsTaxYear);
        Assert.Equal("S,Q1,L1", walk.Path);
        Assert.Equal("L1", walk.LeafId);
        Assert.Null(walk.PurposeNote);
        Assert.Equal(Now.UtcDateTime, walk.CreatedAt);
    }

    [Fact]
    public async Task Submit_ReturnsTheDebtLeaf_WithGoldenFigures_AndEverythingTheResultCardShows()
    {
        await using AppDbContext db = CreateContext();

        PriorityWalkResultDto result = await CreateService(db).SubmitAsync(UserId, DebtRequest());

        Assert.Equal("L1", result.LeafId);
        Assert.Equal("Pay down the high-interest debt first", result.LeafLabel);
        Assert.Equal("Pay down the high-interest debt first.", result.HeadlineLeafLabel);
        Assert.Equal(500m, result.MonthlyAmount);
        Assert.Equal(Now.UtcDateTime, result.CreatedAtUtc);
        Assert.Equal(1, result.TreeVersion);

        PriorityDebtFiguresDto debt = Assert.IsType<PriorityDebtFiguresDto>(result.Figures.Debt);
        Assert.Equal(PriorityDebtOutcome.PaidOff, debt.Outcome);
        Assert.Equal(12, debt.Months);
        Assert.Equal(574.44m, decimal.Round(debt.TotalInterest!.Value, 2));
        Assert.Equal(74.44m, decimal.Round(debt.FinalPayment!.Value, 2));
        Assert.NotEqual(decimal.Round(debt.TotalInterest.Value, 2), debt.TotalInterest.Value); // full precision
        Assert.Equal(22m, debt.AprPercent);
        Assert.Equal(7m, debt.AssumedReturnPercent);
        Assert.Null(result.Figures.EmergencyFund);
        Assert.Empty(result.Figures.Scenarios);

        Assert.Equal(["S", "Q1", "L1"], result.PathNodeIds);
        Assert.Equal(1, result.Tree.Version);
        Assert.Equal(13, result.Tree.Nodes.Count);
        Assert.Equal(12, result.Tree.Edges.Count);
        Assert.Null(result.LimitsTaxYear);
        Assert.Equal(MoneyPrioritiesMessages.EstimateDisclaimer, result.Disclaimer);
        Assert.Equal(7m, result.Assumptions.BaseReturnPercent);
        Assert.Equal(4m, result.Assumptions.ConservativeReturnPercent);
        Assert.Equal(10m, result.Assumptions.AggressiveReturnPercent);
        Assert.Equal(3.5m, result.Assumptions.CashYieldPercent);
        Assert.Equal(5_000m, result.Answers.DebtBalance);
        Assert.Null(result.Answers.EmergencyFundMonths);
    }

    [Fact]
    public async Task Submit_TenYearHorizon_ProjectsTheGoldenBaseBalance()
    {
        await using AppDbContext db = CreateContext();

        PriorityWalkResultDto result = await CreateService(db).SubmitAsync(UserId, HorizonRequest(10));

        Assert.Equal("L5", result.LeafId);
        Assert.Equal(
            [PriorityGrowthScenario.Conservative, PriorityGrowthScenario.Base, PriorityGrowthScenario.Aggressive],
            result.Figures.Scenarios.Select(s => s.Scenario));
        PriorityScenarioBalanceDto baseCase = result.Figures.Scenarios.Single(s => s.Scenario == PriorityGrowthScenario.Base);
        Assert.Equal(7m, baseCase.RatePercent);
        Assert.Equal(86_542.40m, decimal.Round(baseCase.Balance, 2));
        Assert.Equal(60_000m, result.Figures.TotalContributed);
        Assert.Equal(10, result.Figures.HorizonYears);
        Assert.Null(result.LimitsTaxYear);
    }

    [Fact]
    public async Task Submit_LimitQuestionOnThePath_StoresTheAgeBandName_AndTheLimitsYear()
    {
        await using AppDbContext db = CreateContext();

        PriorityWalkResultDto result = await CreateService(db).SubmitAsync(UserId, HorizonRequest(25));

        PriorityWalk walk = Assert.Single(await db.PriorityWalks.ToListAsync());
        Assert.Equal("Age60To63", walk.AgeBand);
        Assert.False(walk.AtContributionLimit);
        Assert.Equal(2026, walk.LimitsTaxYear);
        Assert.Equal("S,Q1,Q2,Q3,Q4,Q5,L6", walk.Path);

        Assert.Equal(2026, result.LimitsTaxYear);
        PriorityRetirementSplitDto split = Assert.IsType<PriorityRetirementSplitDto>(result.Figures.RetirementSplit);
        Assert.Equal(2026, split.TaxYear);
        Assert.Equal(RetirementAgeBand.Age60To63, split.AgeBand);
        Assert.Equal(6_000m, split.YearlyAmount);
        Assert.Equal(6_000m, split.ToIra);
        Assert.Equal(0m, split.ToEmployeePlan);
        Assert.Equal(8_600m, split.IraLimit);
        Assert.Equal(35_750m, split.EmployeeDeferralLimit);
        Assert.Equal(153_000m, split.RothPhaseOutSingleStart);
        Assert.Equal(RetirementAgeBand.Age60To63, result.Answers.AgeBand);
    }

    [Fact]
    public async Task Submit_InAYearWithNoLimitsRow_UsesTheLatestRow()
    {
        await using AppDbContext db = CreateContext();

        PriorityWalkResultDto result = await CreateService(db, new DateTimeOffset(2031, 1, 5, 0, 0, 0, TimeSpan.Zero))
            .SubmitAsync(UserId, HorizonRequest(25));

        Assert.Equal(RetirementContributionLimits.Latest.TaxYear, result.LimitsTaxYear);
        Assert.Equal(RetirementContributionLimits.Latest.TaxYear, (await db.PriorityWalks.SingleAsync()).LimitsTaxYear);
    }

    [Fact]
    public async Task Submit_DebtThatTakesLongerThanTheMaximum_ReturnsThatOutcome_WithoutFigures()
    {
        await using AppDbContext db = CreateContext();
        var request = new SubmitPriorityWalkRequest { MonthlyAmount = 100.01m, DebtApr = 12m, DebtBalance = 10_000m };

        PriorityWalkResultDto result = await CreateService(db).SubmitAsync(UserId, request);

        PriorityDebtFiguresDto debt = Assert.IsType<PriorityDebtFiguresDto>(result.Figures.Debt);
        Assert.Equal(PriorityDebtOutcome.LongerThanMaximum, debt.Outcome);
        Assert.Equal(600, debt.MaximumMonths);
        Assert.Equal(50, debt.MaximumYears);
        Assert.Null(debt.Months);
        Assert.Null(debt.TotalInterest);
        Assert.Null(debt.FinalPayment);
    }

    [Fact]
    public async Task Submit_AmountAtAMonthsInterest_SaysItDoesNotCoverTheInterest()
    {
        await using AppDbContext db = CreateContext();
        var request = new SubmitPriorityWalkRequest { MonthlyAmount = 100m, DebtApr = 12m, DebtBalance = 10_000m };

        PriorityWalkResultDto result = await CreateService(db).SubmitAsync(UserId, request);

        PriorityDebtFiguresDto debt = Assert.IsType<PriorityDebtFiguresDto>(result.Figures.Debt);
        Assert.Equal(PriorityDebtOutcome.AmountDoesNotCoverInterest, debt.Outcome);
        Assert.Equal(100m, debt.FirstMonthInterest);
        Assert.Null(debt.Months);
    }

    [Fact]
    public async Task Submit_IdenticalToTheLatestWalk_IsNotSavedAgain_AndReturnsThatWalk()
    {
        await using AppDbContext db = CreateContext();
        MoneyPrioritiesService service = CreateService(db);
        SubmitPriorityWalkRequest first = DebtRequest();
        first.Purpose = "Debt free";

        PriorityWalkResultDto saved = await service.SubmitAsync(UserId, first);
        SubmitPriorityWalkRequest again = DebtRequest();
        again.Purpose = "Debt free";
        again.HorizonYears = 40; // off the path: ignored, so still identical
        PriorityWalkResultDto repeated = await service.SubmitAsync(UserId, again);

        Assert.Single(await db.PriorityWalks.ToListAsync());
        Assert.Equal(saved.WalkId, repeated.WalkId);
        Assert.Equal(12, repeated.Figures.Debt!.Months);
    }

    [Fact]
    public async Task Submit_ADifferentPurposeOrAnswer_SavesANewWalk()
    {
        await using AppDbContext db = CreateContext();
        MoneyPrioritiesService service = CreateService(db);

        await service.SubmitAsync(UserId, DebtRequest());
        SubmitPriorityWalkRequest withPurpose = DebtRequest();
        withPurpose.Purpose = "Debt free";
        await service.SubmitAsync(UserId, withPurpose);
        SubmitPriorityWalkRequest otherBalance = DebtRequest();
        otherBalance.Purpose = "Debt free";
        otherBalance.DebtBalance = 5_000.01m;
        await service.SubmitAsync(UserId, otherBalance);

        Assert.Equal(3, await db.PriorityWalks.CountAsync());
    }

    [Fact]
    public async Task Submit_ComparesWithTheLatestWalkOnly_AndWithThisAccountsWalksOnly()
    {
        await using AppDbContext db = CreateContext();
        MoneyPrioritiesService service = CreateService(db);

        await service.SubmitAsync(UserId, DebtRequest());
        await service.SubmitAsync(UserId, HorizonRequest(5));
        await service.SubmitAsync(UserId, DebtRequest());
        await service.SubmitAsync(OtherUserId, DebtRequest());

        Assert.Equal(3, await db.PriorityWalks.CountAsync(w => w.UserId == UserId));
        Assert.Equal(1, await db.PriorityWalks.CountAsync(w => w.UserId == OtherUserId));
    }

    [Fact]
    public async Task Submit_IdenticalToASoftDeletedLatestWalk_SavesANewWalk()
    {
        await using AppDbContext db = CreateContext();
        MoneyPrioritiesService service = CreateService(db);
        PriorityWalkResultDto first = await service.SubmitAsync(UserId, DebtRequest());
        await service.DeleteAsync(UserId, first.WalkId);

        PriorityWalkResultDto second = await service.SubmitAsync(UserId, DebtRequest());

        Assert.NotEqual(first.WalkId, second.WalkId);
        Assert.Single(await db.PriorityWalks.ToListAsync());
    }

    [Fact]
    public async Task ListRecent_ReturnsTheTenNewest_NewestFirst_ForThisAccountOnly()
    {
        await using AppDbContext db = CreateContext();
        DateTime start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 12; i++)
        {
            db.PriorityWalks.Add(Walk(UserId, start.AddDays(i), 100m + i));
        }

        db.PriorityWalks.Add(Walk(OtherUserId, start.AddDays(30), 999m));
        PriorityWalk deleted = Walk(UserId, start.AddDays(40), 777m);
        deleted.SoftDelete();
        db.PriorityWalks.Add(deleted);
        await db.SaveChangesAsync();

        List<PriorityWalkSummaryDto> recent = await CreateService(db).ListRecentAsync(UserId);

        Assert.Equal(10, recent.Count);
        Assert.Equal(Enumerable.Range(2, 10).Reverse().Select(i => 100m + i), recent.Select(r => r.MonthlyAmount));
        Assert.Equal(start.AddDays(11), recent[0].CreatedAtUtc);
        Assert.All(recent, r => Assert.Equal("L5", r.LeafId));
        Assert.All(recent, r => Assert.Equal("Taxable brokerage: balanced index funds", r.LeafLabel));
    }

    [Fact]
    public async Task ListRecent_BreaksATieOnCreatedAt_ByTheNewerId()
    {
        await using AppDbContext db = CreateContext();
        DateTime at = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        db.PriorityWalks.AddRange(Walk(UserId, at, 1m), Walk(UserId, at, 2m));
        await db.SaveChangesAsync();

        List<PriorityWalkSummaryDto> recent = await CreateService(db).ListRecentAsync(UserId);

        Assert.True(recent[0].WalkId > recent[1].WalkId);
    }

    [Fact]
    public async Task Get_RecomputesTheFiguresFromTheStoredAnswers()
    {
        await using AppDbContext db = CreateContext();
        PriorityWalk stored = Walk(UserId, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 500m);
        stored.Purpose = "Retire early";
        db.PriorityWalks.Add(stored);
        await db.SaveChangesAsync();

        PriorityWalkResultDto? result = await CreateService(db).GetAsync(UserId, stored.Id);

        Assert.NotNull(result);
        Assert.Equal(stored.Id, result.WalkId);
        Assert.Equal("L5", result.LeafId);
        Assert.Equal("Retire early", result.Purpose);
        Assert.Equal(86_542.40m,
            decimal.Round(result.Figures.Scenarios.Single(s => s.Scenario == PriorityGrowthScenario.Base).Balance, 2));
        Assert.Equal(["S", "Q1", "Q2", "Q3", "Q4", "L5"], result.PathNodeIds);
        Assert.Equal(10, result.Answers.HorizonYears);
    }

    [Fact]
    public async Task Get_AnL6Walk_UsesItsStoredLimitsYear_AndParsesItsAgeBand()
    {
        await using AppDbContext db = CreateContext();
        PriorityWalk stored = Walk(UserId, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 3_000m);
        stored.HorizonYears = 30;
        stored.AgeBand = nameof(RetirementAgeBand.Age50To59Or64Up);
        stored.AtContributionLimit = false;
        stored.LimitsTaxYear = 2026;
        stored.Path = "S,Q1,Q2,Q3,Q4,Q5,L6";
        stored.LeafId = "L6";
        db.PriorityWalks.Add(stored);
        await db.SaveChangesAsync();

        PriorityWalkResultDto? result = await CreateService(db, new DateTimeOffset(2031, 6, 1, 0, 0, 0, TimeSpan.Zero))
            .GetAsync(UserId, stored.Id);

        Assert.NotNull(result);
        Assert.Equal(2026, result.LimitsTaxYear);
        PriorityRetirementSplitDto split = Assert.IsType<PriorityRetirementSplitDto>(result.Figures.RetirementSplit);
        Assert.Equal(RetirementAgeBand.Age50To59Or64Up, split.AgeBand);
        Assert.Equal(36_000m, split.YearlyAmount);
        Assert.Equal(8_600m, split.ToIra);
        Assert.Equal(27_400m, split.ToEmployeePlan);
        Assert.Equal(0m, split.ToTaxable);
    }

    [Fact]
    public async Task Get_AnotherAccountsWalk_OrADeletedOne_IsNotFound()
    {
        await using AppDbContext db = CreateContext();
        PriorityWalk theirs = Walk(OtherUserId, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 500m);
        PriorityWalk deleted = Walk(UserId, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 500m);
        deleted.SoftDelete();
        db.PriorityWalks.AddRange(theirs, deleted);
        await db.SaveChangesAsync();
        MoneyPrioritiesService service = CreateService(db);

        Assert.Null(await service.GetAsync(UserId, theirs.Id));
        Assert.Null(await service.GetAsync(UserId, deleted.Id));
        Assert.Null(await service.GetAsync(UserId, 9_999));
    }

    [Fact]
    public async Task Delete_SoftDeletesTheWalk()
    {
        await using AppDbContext db = CreateContext();
        PriorityWalk mine = Walk(UserId, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 500m);
        db.PriorityWalks.Add(mine);
        await db.SaveChangesAsync();
        MoneyPrioritiesService service = CreateService(db);

        Assert.True(await service.DeleteAsync(UserId, mine.Id));

        PriorityWalk row = await db.PriorityWalks.IgnoreQueryFilters().SingleAsync();
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
        Assert.Empty(await service.ListRecentAsync(UserId));
        Assert.False(await service.DeleteAsync(UserId, mine.Id));
    }

    [Fact]
    public async Task Delete_AnotherAccountsWalk_ReturnsFalse_AndKeepsIt()
    {
        await using AppDbContext db = CreateContext();
        PriorityWalk theirs = Walk(OtherUserId, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 500m);
        db.PriorityWalks.Add(theirs);
        await db.SaveChangesAsync();

        Assert.False(await CreateService(db).DeleteAsync(UserId, theirs.Id));
        Assert.False((await db.PriorityWalks.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public void GetTree_ReturnsTheAuthoredTree_InAuthoredOrder()
    {
        using AppDbContext db = CreateContext();

        PriorityTreeDiagramDto tree = CreateService(db).GetTree();

        Assert.Equal(1, tree.TreeVersion);
        Assert.Equal(1, tree.Tree.Version);
        Assert.Equal(PriorityTreeV1.Tree.Nodes.Select(node => (node.Id, node.Kind, node.Label)),
            tree.Tree.Nodes.Select(node => (node.Id, node.Kind, node.Label)));
        Assert.Equal(PriorityTreeV1.Tree.Edges.Select(edge => (edge.From, edge.To, edge.Label)),
            tree.Tree.Edges.Select(edge => (edge.From, edge.To, edge.Label)));
    }

    [Fact]
    public void Queries_TranslateToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        MoneyPrioritiesService service = MoneyPrioritiesTestServices.Service(db);

        string recent = AzureSqlTranslation.AssertTranslates(service.RecentWalksQuery(UserId));
        string latest = AzureSqlTranslation.AssertTranslates(service.LatestWalkQuery(UserId));

        Assert.Contains("ORDER BY [p].[CreatedAt] DESC, [p].[Id] DESC", recent, StringComparison.Ordinal);
        Assert.Contains("TOP", latest, StringComparison.Ordinal);

        string noteCandidates = AzureSqlTranslation.AssertTranslates(service.NoteCandidatesQuery(UserId,
            new PriorityWalk { Id = 7, Purpose = "A house", TreeVersion = 1, LeafId = "L1", MonthlyAmount = 500m }));
        Assert.Contains("[p].[PurposeNote] IS NOT NULL", noteCandidates, StringComparison.Ordinal);
    }

    /// <summary>A stored L5 Walk: no debt, fund covered, full match, 10 years.</summary>
    private static PriorityWalk Walk(string userId, DateTime createdAt, decimal amount) => new()
    {
        UserId = userId,
        CreatedAt = createdAt,
        MonthlyAmount = amount,
        DebtApr = 0m,
        EmergencyFundMonths = 6m,
        HasFullEmployerMatch = true,
        HorizonYears = 10,
        TreeVersion = 1,
        Path = "S,Q1,Q2,Q3,Q4,L5",
        LeafId = "L5"
    };
}
