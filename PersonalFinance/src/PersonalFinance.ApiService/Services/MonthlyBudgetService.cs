using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Domain service implementing Monthly Budget Tracker business rules, sheet calculations, category management, and transactions.
/// </summary>
public class MonthlyBudgetService : IMonthlyBudgetService
{
    private static readonly string[] DefaultExpenseCategories =
    [
        "Food",
        "Gifts",
        "Health/medical",
        "Home",
        "Transportation",
        "Personal",
        "Pets",
        "Utilities",
        "Travel",
        "Debt",
        "Other"
    ];

    private static readonly string[] DefaultIncomeCategories =
    [
        "Savings",
        "Paycheck",
        "Bonus",
        "Interest",
        "Other"
    ];

    private readonly AppDbContext _dbContext;

    public MonthlyBudgetService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task EnsureDefaultCategoriesAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var hasAnyCategories = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .AnyAsync(c => c.UserId == userId, ct);

        if (hasAnyCategories)
        {
            return;
        }

        var categoriesToAdd = new List<BudgetCategory>();

        for (var i = 0; i < DefaultExpenseCategories.Length; i++)
        {
            categoriesToAdd.Add(new BudgetCategory
            {
                UserId = userId,
                Name = DefaultExpenseCategories[i],
                Kind = BudgetCategoryKind.Expense,
                SortOrder = (i + 1) * 10
            });
        }

        for (var i = 0; i < DefaultIncomeCategories.Length; i++)
        {
            categoriesToAdd.Add(new BudgetCategory
            {
                UserId = userId,
                Name = DefaultIncomeCategories[i],
                Kind = BudgetCategoryKind.Income,
                SortOrder = (i + 1) * 10
            });
        }

        try
        {
            _dbContext.BudgetCategories.AddRange(categoriesToAdd);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            foreach (var entity in categoriesToAdd)
            {
                var entry = _dbContext.Entry(entity);
                if (entry.State != EntityState.Detached)
                {
                    entry.State = EntityState.Detached;
                }
            }

            var countAfter = await _dbContext.BudgetCategories
                .IgnoreQueryFilters()
                .CountAsync(c => c.UserId == userId, ct);

            if (countAfter == 0)
            {
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetSheetDto> GetSheetAsync(string userId, int year, int month,
        CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesAsync(userId, ct);

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var allCategories = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var categoryMap = allCategories.ToDictionary(c => c.Id);

        var transactions = await _dbContext.BudgetTransactions
            .Where(t => t.UserId == userId && t.Date >= startDate && t.Date < endDate)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        var txSumByCategory = transactions
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var plan = await _dbContext.MonthlyBudgetPlans
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == year && p.Month == month, ct);

        var lineMap = plan?.Lines.ToDictionary(l => l.CategoryId) ?? new Dictionary<int, MonthlyBudgetLine>();

        var isInitialized = plan != null;
        var canCopyFromPrevious = false;
        string? previousMonthLabel = null;

        if (!isInitialized)
        {
            var earlierPlan = await _dbContext.MonthlyBudgetPlans
                .Where(p => p.UserId == userId && (p.Year < year || (p.Year == year && p.Month < month)))
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .FirstOrDefaultAsync(ct);

            if (earlierPlan != null)
            {
                canCopyFromPrevious = true;
                previousMonthLabel =
                    new DateTime(earlierPlan.Year, earlierPlan.Month, 1).ToString("MMMM yyyy",
                        CultureInfo.InvariantCulture);
            }
        }

        // Build calculated category rows using the unified calculation rules
        var calculatedRows = MonthlyBudgetCalculator.BuildRows(allCategories, lineMap, txSumByCategory);

        var expenseRows = calculatedRows
            .Where(r => r.Kind == BudgetCategoryKind.Expense)
            .Select(r => new SheetRowDto
            {
                CategoryId = r.CategoryId,
                Name = r.Name,
                Planned = r.Planned,
                Actual = r.Actual,
                Diff = r.Diff,
                ActualIsOverride = r.ActualIsOverride,
                IsArchived = r.IsArchived
            })
            .ToList();

        var incomeRows = calculatedRows
            .Where(r => r.Kind == BudgetCategoryKind.Income)
            .Select(r => new SheetRowDto
            {
                CategoryId = r.CategoryId,
                Name = r.Name,
                Planned = r.Planned,
                Actual = r.Actual,
                Diff = r.Diff,
                ActualIsOverride = r.ActualIsOverride,
                IsArchived = r.IsArchived
            })
            .ToList();

        var expenseTotalPlanned = expenseRows.Sum(r => r.Planned);
        var expenseTotalActual = expenseRows.Sum(r => r.Actual);
        var expenseTotalDiff = expenseTotalPlanned - expenseTotalActual;

        var incomeTotalPlanned = incomeRows.Sum(r => r.Planned);
        var incomeTotalActual = incomeRows.Sum(r => r.Actual);
        var incomeTotalDiff = incomeTotalActual - incomeTotalPlanned;

        var transactionDtos = transactions.Select(t =>
        {
            categoryMap.TryGetValue(t.CategoryId, out var cat);
            return new BudgetTransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                Description = t.Description,
                CategoryId = t.CategoryId,
                CategoryName = cat?.Name ?? "Unknown",
                Kind = cat?.Kind ?? BudgetCategoryKind.Expense
            };
        }).ToList();

        return new MonthlyBudgetSheetDto
        {
            Year = year,
            Month = month,
            MonthName = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            IsInitialized = isInitialized,
            CanCopyFromPrevious = canCopyFromPrevious,
            PreviousMonthLabel = previousMonthLabel,
            Expenses = new SheetSectionDto
            {
                Rows = expenseRows,
                TotalPlanned = expenseTotalPlanned,
                TotalActual = expenseTotalActual,
                TotalDiff = expenseTotalDiff
            },
            Income = new SheetSectionDto
            {
                Rows = incomeRows,
                TotalPlanned = incomeTotalPlanned,
                TotalActual = incomeTotalActual,
                TotalDiff = incomeTotalDiff
            },
            Transactions = transactionDtos
        };
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetSheetDto> InitializeMonthAsync(string userId, InitializeMonthRequest request,
        CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesAsync(userId, ct);

        var existingPlan = await _dbContext.MonthlyBudgetPlans
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == request.Year && p.Month == request.Month, ct);

        if (existingPlan == null)
        {
            var plan = new MonthlyBudgetPlan
            {
                UserId = userId,
                Year = request.Year,
                Month = request.Month,
                Lines = new List<MonthlyBudgetLine>()
            };

            if (request.CopyFromPrevious)
            {
                var earlierPlan = await _dbContext.MonthlyBudgetPlans
                    .Include(p => p.Lines)
                    .Where(p => p.UserId == userId &&
                                (p.Year < request.Year || (p.Year == request.Year && p.Month < request.Month)))
                    .OrderByDescending(p => p.Year)
                    .ThenByDescending(p => p.Month)
                    .FirstOrDefaultAsync(ct);

                if (earlierPlan != null)
                {
                    // Copy ONLY lines for active (non-archived) categories belonging to this user
                    var activeCategoryIds = await _dbContext.BudgetCategories
                        .Where(c => c.UserId == userId)
                        .Select(c => c.Id)
                        .ToHashSetAsync(ct);

                    foreach (var line in earlierPlan.Lines.Where(l => activeCategoryIds.Contains(l.CategoryId)))
                    {
                        plan.Lines.Add(new MonthlyBudgetLine
                        {
                            UserId = userId,
                            CategoryId = line.CategoryId,
                            PlannedAmount = line.PlannedAmount,
                            ActualOverride = null
                        });
                    }
                }
            }

            _dbContext.MonthlyBudgetPlans.Add(plan);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                foreach (var line in plan.Lines)
                {
                    var lineEntry = _dbContext.Entry(line);
                    if (lineEntry.State != EntityState.Detached)
                    {
                        lineEntry.State = EntityState.Detached;
                    }
                }

                var planEntry = _dbContext.Entry(plan);
                if (planEntry.State != EntityState.Detached)
                {
                    planEntry.State = EntityState.Detached;
                }

                var reloadedPlan = await _dbContext.MonthlyBudgetPlans
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == request.Year && p.Month == request.Month,
                        ct);

                if (reloadedPlan == null)
                {
                    throw;
                }
            }
        }

        return await GetSheetAsync(userId, request.Year, request.Month, ct);
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetSheetDto> SetPlannedAsync(string userId, int year, int month, int categoryId,
        SetPlannedRequest request, CancellationToken ct = default)
    {
        var category = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);

        if (category == null)
        {
            throw new KeyNotFoundException("Category not found.");
        }

        await EnsureArchivedCategoryIsEditableInMonthAsync(category, userId, year, month, ct);

        var plan = await GetOrCreatePlanAsync(userId, year, month, ct);

        var line = plan.Lines.FirstOrDefault(l => l.CategoryId == categoryId);
        if (line != null)
        {
            line.PlannedAmount = request.Amount;
            line.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
        else
        {
            var newLine = new MonthlyBudgetLine
            {
                PlanId = plan.Id,
                UserId = userId,
                CategoryId = categoryId,
                PlannedAmount = request.Amount,
                ActualOverride = null
            };
            plan.Lines.Add(newLine);
            _dbContext.MonthlyBudgetLines.Add(newLine);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                var lineEntry = _dbContext.Entry(newLine);
                if (lineEntry.State != EntityState.Detached)
                {
                    lineEntry.State = EntityState.Detached;
                }

                plan.Lines.Remove(newLine);

                var existingLine = await _dbContext.MonthlyBudgetLines
                    .FirstOrDefaultAsync(l => l.PlanId == plan.Id && l.CategoryId == categoryId && l.UserId == userId,
                        ct);

                if (existingLine != null)
                {
                    existingLine.PlannedAmount = request.Amount;
                    existingLine.UpdatedAt = DateTime.UtcNow;
                    if (!plan.Lines.Contains(existingLine))
                    {
                        plan.Lines.Add(existingLine);
                    }

                    await _dbContext.SaveChangesAsync(ct);
                }
                else
                {
                    throw;
                }
            }
        }

        return await GetSheetAsync(userId, year, month, ct);
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetSheetDto> SetActualOverrideAsync(string userId, int year, int month, int categoryId,
        SetActualOverrideRequest request, CancellationToken ct = default)
    {
        var category = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);

        if (category == null)
        {
            throw new KeyNotFoundException("Category not found.");
        }

        await EnsureArchivedCategoryIsEditableInMonthAsync(category, userId, year, month, ct);

        var plan = await GetOrCreatePlanAsync(userId, year, month, ct);

        var line = plan.Lines.FirstOrDefault(l => l.CategoryId == categoryId);
        if (line != null)
        {
            line.ActualOverride = request.Amount;
            line.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
        else
        {
            var newLine = new MonthlyBudgetLine
            {
                PlanId = plan.Id,
                UserId = userId,
                CategoryId = categoryId,
                PlannedAmount = 0m,
                ActualOverride = request.Amount
            };
            plan.Lines.Add(newLine);
            _dbContext.MonthlyBudgetLines.Add(newLine);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                var lineEntry = _dbContext.Entry(newLine);
                if (lineEntry.State != EntityState.Detached)
                {
                    lineEntry.State = EntityState.Detached;
                }

                plan.Lines.Remove(newLine);

                var existingLine = await _dbContext.MonthlyBudgetLines
                    .FirstOrDefaultAsync(l => l.PlanId == plan.Id && l.CategoryId == categoryId && l.UserId == userId,
                        ct);

                if (existingLine != null)
                {
                    existingLine.ActualOverride = request.Amount;
                    existingLine.UpdatedAt = DateTime.UtcNow;
                    if (!plan.Lines.Contains(existingLine))
                    {
                        plan.Lines.Add(existingLine);
                    }

                    await _dbContext.SaveChangesAsync(ct);
                }
                else
                {
                    throw;
                }
            }
        }

        return await GetSheetAsync(userId, year, month, ct);
    }

    /// <inheritdoc />
    public async Task<List<BudgetCategoryDto>> GetCategoriesAsync(string userId, bool includeArchived = false,
        CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesAsync(userId, ct);

        var query = _dbContext.BudgetCategories.AsQueryable();

        if (includeArchived)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(c => new BudgetCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Kind = c.Kind,
                SortOrder = c.SortOrder,
                IsArchived = c.IsDeleted
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BudgetCategoryDto> CreateCategoryAsync(string userId, SaveCategoryRequest request,
        CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesAsync(userId, ct);

        var name = request.Name.Trim();
        var duplicate = await _dbContext.BudgetCategories
            .AnyAsync(c => c.UserId == userId && c.Kind == request.Kind && c.Name.ToLower() == name.ToLower(), ct);

        if (duplicate)
        {
            throw new DuplicateCategoryException($"Category '{name}' already exists for {request.Kind}.");
        }

        var category = new BudgetCategory
        {
            UserId = userId,
            Name = name,
            Kind = request.Kind,
            SortOrder = request.SortOrder
        };

        _dbContext.BudgetCategories.Add(category);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var entry = _dbContext.Entry(category);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }

            var existing = await _dbContext.BudgetCategories
                .AnyAsync(c => c.UserId == userId && c.Kind == request.Kind && c.Name.ToLower() == name.ToLower(), ct);

            if (existing)
            {
                throw new DuplicateCategoryException($"Category '{name}' already exists for {request.Kind}.");
            }

            throw;
        }

        return new BudgetCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Kind = category.Kind,
            SortOrder = category.SortOrder,
            IsArchived = category.IsDeleted
        };
    }

    /// <summary>
    /// Updates an existing category (name, sort order). Category kind is immutable.
    /// </summary>
    public async Task<BudgetCategoryDto> UpdateCategoryAsync(string userId, int id, SaveCategoryRequest request,
        CancellationToken ct = default)
    {
        var category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (category == null)
        {
            throw new KeyNotFoundException("Category not found.");
        }

        if (request.Kind != category.Kind)
        {
            throw new ArgumentException("Category kind cannot be changed.");
        }

        var name = request.Name.Trim();
        var duplicate = await _dbContext.BudgetCategories
            .AnyAsync(
                c => c.Id != id && c.UserId == userId && c.Kind == category.Kind && c.Name.ToLower() == name.ToLower(),
                ct);

        if (duplicate)
        {
            throw new DuplicateCategoryException($"Category '{name}' already exists for {category.Kind}.");
        }

        category.Name = name;
        category.SortOrder = request.SortOrder;
        category.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return new BudgetCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Kind = category.Kind,
            SortOrder = category.SortOrder,
            IsArchived = category.IsDeleted
        };
    }

    /// <inheritdoc />
    public async Task DeleteCategoryAsync(string userId, int id, CancellationToken ct = default)
    {
        var category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (category == null)
        {
            throw new KeyNotFoundException("Category not found.");
        }

        category.SoftDelete();

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BudgetTransactionDto> CreateTransactionAsync(string userId, SaveTransactionRequest request,
        CancellationToken ct = default)
    {
        var category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

        if (category == null)
        {
            var archivedCat = await _dbContext.BudgetCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

            if (archivedCat != null)
            {
                throw new ArgumentException("Cannot add transaction to an archived category.");
            }

            throw new KeyNotFoundException("Category not found.");
        }

        var transaction = new BudgetTransaction
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description?.Trim() ?? string.Empty
        };

        _dbContext.BudgetTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(ct);

        return new BudgetTransactionDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Amount = transaction.Amount,
            Description = transaction.Description,
            CategoryId = transaction.CategoryId,
            CategoryName = category.Name,
            Kind = category.Kind
        };
    }

    /// <inheritdoc />
    public async Task<BudgetTransactionDto> UpdateTransactionAsync(string userId, int id,
        SaveTransactionRequest request, CancellationToken ct = default)
    {
        var transaction = await _dbContext.BudgetTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        if (transaction == null)
        {
            throw new KeyNotFoundException("Transaction not found.");
        }

        var category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

        if (category == null)
        {
            var archivedCat = await _dbContext.BudgetCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

            if (archivedCat != null)
            {
                throw new ArgumentException("Cannot assign transaction to an archived category.");
            }

            throw new KeyNotFoundException("Category not found.");
        }

        transaction.CategoryId = request.CategoryId;
        transaction.Date = request.Date;
        transaction.Amount = request.Amount;
        transaction.Description = request.Description?.Trim() ?? string.Empty;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return new BudgetTransactionDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Amount = transaction.Amount,
            Description = transaction.Description,
            CategoryId = transaction.CategoryId,
            CategoryName = category.Name,
            Kind = category.Kind
        };
    }

    /// <inheritdoc />
    public async Task DeleteTransactionAsync(string userId, int id, CancellationToken ct = default)
    {
        var transaction = await _dbContext.BudgetTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        if (transaction == null)
        {
            throw new KeyNotFoundException("Transaction not found.");
        }

        transaction.SoftDelete();

        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<MonthlyBudgetPlan> GetOrCreatePlanAsync(string userId, int year, int month, CancellationToken ct)
    {
        var plan = await _dbContext.MonthlyBudgetPlans
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == year && p.Month == month, ct);

        if (plan != null)
        {
            return plan;
        }

        plan = new MonthlyBudgetPlan
        {
            UserId = userId,
            Year = year,
            Month = month,
            Lines = new List<MonthlyBudgetLine>()
        };

        _dbContext.MonthlyBudgetPlans.Add(plan);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return plan;
        }
        catch (DbUpdateException)
        {
            var entry = _dbContext.Entry(plan);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }

            var existingPlan = await _dbContext.MonthlyBudgetPlans
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == year && p.Month == month, ct);

            if (existingPlan != null)
            {
                return existingPlan;
            }

            throw;
        }
    }

    /// <summary>
    /// Archived categories may be edited only in months that already have a plan line or transactions for them.
    /// The check is read-only so a rejected edit cannot create a month plan.
    /// </summary>
    private async Task EnsureArchivedCategoryIsEditableInMonthAsync(
        BudgetCategory category,
        string userId,
        int year,
        int month,
        CancellationToken ct)
    {
        if (!category.IsDeleted)
        {
            return;
        }

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var existingPlanId = await _dbContext.MonthlyBudgetPlans
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Year == year && p.Month == month)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

        var hasLineUsage = existingPlanId.HasValue
                           && await _dbContext.MonthlyBudgetLines
                               .AsNoTracking()
                               .AnyAsync(l => l.PlanId == existingPlanId.Value
                                              && l.CategoryId == category.Id
                                              && (l.PlannedAmount > 0 || l.ActualOverride.HasValue), ct);

        if (hasLineUsage)
        {
            return;
        }

        var hasTransactions = await _dbContext.BudgetTransactions
            .AnyAsync(t => t.UserId == userId
                           && t.CategoryId == category.Id
                           && t.Date >= startDate
                           && t.Date < endDate, ct);

        if (!hasTransactions)
        {
            throw new KeyNotFoundException("Category not found.");
        }
    }
}
