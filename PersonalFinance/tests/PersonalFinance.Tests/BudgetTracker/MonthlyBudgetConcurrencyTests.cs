using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// Unit tests verifying concurrency recovery paths in MonthlyBudgetService: default category seeding,
/// plan creation, category uniqueness races, and plan line conflict recovery using SaveChangesInterceptor.
/// </summary>
public class MonthlyBudgetConcurrencyTests
{
    private const string UserId = "user-concurrency";

    private static (AppDbContext Context, ConcurrencySimulationInterceptor Interceptor, string DbName)
        CreateContextWithInterceptor()
    {
        string dbName = $"BudgetConcurrency_{Guid.NewGuid():N}";
        var interceptor = new ConcurrencySimulationInterceptor();

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

        return (new AppDbContext(options), interceptor, dbName);
    }

    private static AppDbContext CreateSecondaryContext(string dbName)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnsureDefaultCategories_RecoversFromConcurrentSeeding_WithoutThrowingOrStaleEntities()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string? dbName) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);

            // On save, simulate another concurrent thread completing seeding first and throwing DbUpdateException
            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = async _ =>
            {
                await using AppDbContext secondaryDb = CreateSecondaryContext(dbName);
                var secondaryService = new MonthlyBudgetService(secondaryDb);
                await secondaryService.EnsureDefaultCategoriesAsync(UserId);
            };

            // Seeding should catch DbUpdateException, detach failed entities, verify categories exist, and succeed
            await service.EnsureDefaultCategoriesAsync(UserId);

            // Assert exactly 16 categories exist
            int totalCategories = await db.BudgetCategories.CountAsync(c => c.UserId == UserId);
            Assert.Equal(16, totalCategories);

            // Assert a subsequent create operation on the same context succeeds (proving no stale tracked entities)
            BudgetCategoryDto created = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
            {
                Name = "Custom Hobby",
                Kind = BudgetCategoryKind.Expense,
                SortOrder = 200
            });

            Assert.NotNull(created);
            Assert.Equal("Custom Hobby", created.Name);
        }
    }

    [Fact]
    public async Task EnsureDefaultCategories_Rethrows_WhenNoCompetingRowsExist()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string _) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);

            // Simulate DbUpdateException without inserting any competing categories
            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = _ => ValueTask.CompletedTask;

            await Assert.ThrowsAsync<DbUpdateException>(() => service.EnsureDefaultCategoriesAsync(UserId));
        }
    }

    [Fact]
    public async Task InitializeMonth_RecoversFromConcurrentPlanCreation()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string? dbName) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);
            await service.EnsureDefaultCategoriesAsync(UserId);

            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = async _ =>
            {
                await using AppDbContext secondaryDb = CreateSecondaryContext(dbName);
                var secondaryService = new MonthlyBudgetService(secondaryDb);
                await secondaryService.InitializeMonthAsync(UserId, new InitializeMonthRequest
                {
                    Year = 2026,
                    Month = 10,
                    CopyFromPrevious = false
                });
            };

            MonthlyBudgetSheetDto sheet = await service.InitializeMonthAsync(UserId, new InitializeMonthRequest
            {
                Year = 2026,
                Month = 10,
                CopyFromPrevious = false
            });

            Assert.True(sheet.IsInitialized);
            int planCount =
                await db.MonthlyBudgetPlans.CountAsync(p => p.UserId == UserId && p.Year == 2026 && p.Month == 10);
            Assert.Equal(1, planCount);
        }
    }

    [Fact]
    public async Task SetPlanned_RecoversFromConcurrentPlanCreation()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string? dbName) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);
            await service.EnsureDefaultCategoriesAsync(UserId);
            BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = async _ =>
            {
                await using AppDbContext secondaryDb = CreateSecondaryContext(dbName);
                var secondaryPlan = new MonthlyBudgetPlan
                {
                    UserId = UserId,
                    Year = 2026,
                    Month = 11,
                    Lines = new List<MonthlyBudgetLine>()
                };
                secondaryDb.MonthlyBudgetPlans.Add(secondaryPlan);
                await secondaryDb.SaveChangesAsync();
            };

            // Set planned creates plan, hits simulated race, reloads existing plan, and sets planned
            MonthlyBudgetSheetDto sheet = await service.SetPlannedAsync(UserId, 2026, 11, food.Id,
                new SetPlannedRequest { Amount = 450m });

            Assert.True(sheet.IsInitialized);
            SheetRowDto foodRow = sheet.Expenses.Rows.Single(r => r.CategoryId == food.Id);
            Assert.Equal(450m, foodRow.Planned);

            int planCount =
                await db.MonthlyBudgetPlans.CountAsync(p => p.UserId == UserId && p.Year == 2026 && p.Month == 11);
            Assert.Equal(1, planCount);
        }
    }

    [Fact]
    public async Task CreateCategory_RecoversFromConcurrentDuplicate_ThrowingDuplicateCategoryException()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string? dbName) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);
            await service.EnsureDefaultCategoriesAsync(UserId);

            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = async _ =>
            {
                await using AppDbContext secondaryDb = CreateSecondaryContext(dbName);
                secondaryDb.BudgetCategories.Add(new BudgetCategory
                {
                    UserId = UserId,
                    Name = "Freelance Consulting",
                    Kind = BudgetCategoryKind.Income,
                    SortOrder = 50
                });
                await secondaryDb.SaveChangesAsync();
            };

            // Attempting to create duplicate concurrently should throw DuplicateCategoryException
            await Assert.ThrowsAsync<DuplicateCategoryException>(() =>
                service.CreateCategoryAsync(UserId, new SaveCategoryRequest
                {
                    Name = "Freelance Consulting",
                    Kind = BudgetCategoryKind.Income,
                    SortOrder = 50
                }));

            // Context should be clean; subsequent valid category creation should succeed
            BudgetCategoryDto nextCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
            {
                Name = "Unique Income",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 60
            });
            Assert.NotNull(nextCat);
        }
    }

    [Fact]
    public async Task SetPlanned_RecoversFromConcurrentLineCreation_UpdatesCompetingLine()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string? dbName) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);
            await service.EnsureDefaultCategoriesAsync(UserId);
            BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

            // Initialize plan first so GetOrCreatePlanAsync does not throw
            await service.InitializeMonthAsync(UserId, new InitializeMonthRequest { Year = 2026, Month = 10 });

            MonthlyBudgetPlan plan =
                await db.MonthlyBudgetPlans.FirstAsync(p => p.UserId == UserId && p.Year == 2026 && p.Month == 10);

            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = async _ =>
            {
                await using AppDbContext secondaryDb = CreateSecondaryContext(dbName);
                secondaryDb.MonthlyBudgetLines.Add(new MonthlyBudgetLine
                {
                    PlanId = plan.Id,
                    UserId = UserId,
                    CategoryId = food.Id,
                    PlannedAmount = 100m,
                    ActualOverride = null
                });
                await secondaryDb.SaveChangesAsync();
            };

            // SetPlanned tries to insert line, encounters conflict, recovers and updates the competing line to 350
            MonthlyBudgetSheetDto sheet = await service.SetPlannedAsync(UserId, 2026, 10, food.Id,
                new SetPlannedRequest { Amount = 350m });

            SheetRowDto foodRow = sheet.Expenses.Rows.Single(r => r.CategoryId == food.Id);
            Assert.Equal(350m, foodRow.Planned);

            MonthlyBudgetLine recoveredLine =
                await db.MonthlyBudgetLines.SingleAsync(l => l.PlanId == plan.Id && l.CategoryId == food.Id);
            Assert.Equal(1,
                await db.MonthlyBudgetLines.CountAsync(l => l.PlanId == plan.Id && l.CategoryId == food.Id));
            Assert.Equal(350m, recoveredLine.PlannedAmount);
            Assert.NotNull(recoveredLine.UpdatedAt);
        }
    }

    [Fact]
    public async Task SetActualOverride_RecoversFromConcurrentLineCreation_UpdatesCompetingLine()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string? dbName) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);
            await service.EnsureDefaultCategoriesAsync(UserId);
            BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

            // Initialize plan first
            await service.InitializeMonthAsync(UserId, new InitializeMonthRequest { Year = 2026, Month = 10 });

            MonthlyBudgetPlan plan =
                await db.MonthlyBudgetPlans.FirstAsync(p => p.UserId == UserId && p.Year == 2026 && p.Month == 10);

            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = async _ =>
            {
                await using AppDbContext secondaryDb = CreateSecondaryContext(dbName);
                secondaryDb.MonthlyBudgetLines.Add(new MonthlyBudgetLine
                {
                    PlanId = plan.Id,
                    UserId = UserId,
                    CategoryId = food.Id,
                    PlannedAmount = 200m,
                    ActualOverride = null
                });
                await secondaryDb.SaveChangesAsync();
            };

            // SetActualOverride tries to insert line, encounters conflict, recovers and sets override to 180
            MonthlyBudgetSheetDto sheet = await service.SetActualOverrideAsync(UserId, 2026, 10, food.Id,
                new SetActualOverrideRequest { Amount = 180m });

            SheetRowDto foodRow = sheet.Expenses.Rows.Single(r => r.CategoryId == food.Id);
            Assert.Equal(180m, foodRow.Actual);
            Assert.True(foodRow.ActualIsOverride);

            MonthlyBudgetLine recoveredLine =
                await db.MonthlyBudgetLines.SingleAsync(l => l.PlanId == plan.Id && l.CategoryId == food.Id);
            Assert.Equal(1,
                await db.MonthlyBudgetLines.CountAsync(l => l.PlanId == plan.Id && l.CategoryId == food.Id));
            Assert.Equal(180m, recoveredLine.ActualOverride);
            Assert.NotNull(recoveredLine.UpdatedAt);
        }
    }

    [Fact]
    public async Task SetPlanned_Rethrows_WhenNoCompetingLineExists()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string _) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new MonthlyBudgetService(db);
            await service.EnsureDefaultCategoriesAsync(UserId);
            BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

            await service.InitializeMonthAsync(UserId, new InitializeMonthRequest { Year = 2026, Month = 10 });

            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = _ => ValueTask.CompletedTask;

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 300m }));
        }
    }

    private sealed class ConcurrencySimulationInterceptor : SaveChangesInterceptor
    {
        public Func<AppDbContext, ValueTask>? OnSavingChangesAsync { get; set; }
        public bool ShouldThrow { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (OnSavingChangesAsync != null && eventData.Context is AppDbContext context)
            {
                Func<AppDbContext, ValueTask> handler = OnSavingChangesAsync;
                OnSavingChangesAsync = null; // Execute only once for the target operation
                await handler(context);
                if (ShouldThrow)
                {
                    throw new DbUpdateException("Simulated concurrency conflict", (Exception?)null);
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
