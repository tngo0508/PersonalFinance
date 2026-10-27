using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.BudgetTracker;

/// <summary>
/// Domain service implementing Monthly Budget Tracker business rules, sheet calculations, category management, and transactions.
/// </summary>
internal sealed class MonthlyBudgetService : IMonthlyBudgetService
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
    private readonly ICurrencyConverter _currencyConverter;

    public MonthlyBudgetService(AppDbContext dbContext, ICurrencyConverter currencyConverter)
    {
        _dbContext = dbContext;
        _currencyConverter = currencyConverter;
    }

    /// <inheritdoc />
    public async Task EnsureDefaultCategoriesAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        // Archived categories still name the live months and transactions that used them, so they count while any
        // live data remains. With none left (a new user, or after a Data Source Removal) the defaults are seeded.
        bool hasCategoriesInUse = await _dbContext.BudgetCategories.AnyAsync(c => c.UserId == userId, ct)
                                  || await _dbContext.MonthlyBudgetPlans.AnyAsync(p => p.UserId == userId, ct)
                                  || await _dbContext.BudgetTransactions.AnyAsync(t => t.UserId == userId, ct);

        if (hasCategoriesInUse)
        {
            return;
        }

        var categoriesToAdd = new List<BudgetCategory>();

        for (int i = 0; i < DefaultExpenseCategories.Length; i++)
        {
            categoriesToAdd.Add(new BudgetCategory
            {
                UserId = userId,
                Name = DefaultExpenseCategories[i],
                Kind = BudgetCategoryKind.Expense,
                SortOrder = (i + 1) * 10
            });
        }

        for (int i = 0; i < DefaultIncomeCategories.Length; i++)
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
            foreach (BudgetCategory entity in categoriesToAdd)
            {
                EntityEntry<BudgetCategory> entry = _dbContext.Entry(entity);
                if (entry.State != EntityState.Detached)
                {
                    entry.State = EntityState.Detached;
                }
            }

            int countAfter = await _dbContext.BudgetCategories
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
        DateOnly endDate = startDate.AddMonths(1);

        List<BudgetCategory> allCategories = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var categoryMap = allCategories.ToDictionary(c => c.Id);

        List<BudgetTransaction> transactions = await _dbContext.BudgetTransactions
            .Where(t => t.UserId == userId && t.Date >= startDate && t.Date < endDate)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        MonthlyBudgetPlan? plan = await _dbContext.MonthlyBudgetPlans
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == year && p.Month == month, ct);

        // Every stored amount is brought into the Reporting Currency (ADR 0016): a transaction at its own day's rate,
        // a line at the month's. The calculator then works on converted figures, so rows and totals add up.
        Dictionary<int, MonthlyBudgetLine> storedLines =
            plan?.Lines.ToDictionary(l => l.CategoryId) ?? new Dictionary<int, MonthlyBudgetLine>();
        string reportingCurrency = await _currencyConverter.GetReportingCurrencyAsync(userId, ct);
        ReportingConversion conversion = await _currencyConverter.PrepareAsync(reportingCurrency,
            transactions.Select(t => new MoneyAt(t.CurrencyCode, t.Date))
                .Concat(storedLines.Values.Select(l => new MoneyAt(l.CurrencyCode, startDate))), ct);

        Dictionary<int, decimal> txSumByCategory = MonthlyBudgetCalculator.TransactionSums(transactions, conversion);
        Dictionary<int, (string CurrencyCode, decimal Sum)?> txOriginalByCategory = transactions
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => OriginalSum(g, conversion));
        Dictionary<int, MonthlyBudgetLine> lineMap = storedLines.ToDictionary(pair => pair.Key,
            pair => MonthlyBudgetCalculator.InReportingCurrency(pair.Value, conversion, startDate));

        bool isInitialized = plan != null;
        bool canCopyFromPrevious = false;
        int? earlierPlanYear = null;
        int? earlierPlanMonth = null;

        if (!isInitialized)
        {
            MonthlyBudgetPlan? earlierPlan = await _dbContext.MonthlyBudgetPlans
                .Where(p => p.UserId == userId && (p.Year < year || (p.Year == year && p.Month < month)))
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .FirstOrDefaultAsync(ct);

            if (earlierPlan != null)
            {
                canCopyFromPrevious = true;
                earlierPlanYear = earlierPlan.Year;
                earlierPlanMonth = earlierPlan.Month;
            }
        }

        // Build calculated category rows using the unified calculation rules
        List<CalculatedBudgetCategoryRow> calculatedRows = MonthlyBudgetCalculator.BuildRows(allCategories, lineMap, txSumByCategory);

        var expenseRows = calculatedRows
            .Where(r => r.Kind == BudgetCategoryKind.Expense)
            .Select(r => ToRow(r, storedLines, txOriginalByCategory, conversion))
            .ToList();

        var incomeRows = calculatedRows
            .Where(r => r.Kind == BudgetCategoryKind.Income)
            .Select(r => ToRow(r, storedLines, txOriginalByCategory, conversion))
            .ToList();

        // Totals are in the Reporting Currency, each rounded once (localization spec §2.5).
        decimal expenseTotalPlanned = conversion.Total(expenseRows.Select(r => r.Planned));
        decimal expenseTotalActual = conversion.Total(expenseRows.Select(r => r.Actual));
        decimal expenseTotalDiff = conversion.Round(expenseTotalPlanned - expenseTotalActual);

        decimal incomeTotalPlanned = conversion.Total(incomeRows.Select(r => r.Planned));
        decimal incomeTotalActual = conversion.Total(incomeRows.Select(r => r.Actual));
        decimal incomeTotalDiff = conversion.Round(incomeTotalActual - incomeTotalPlanned);

        var transactionDtos = transactions.Select(t =>
        {
            categoryMap.TryGetValue(t.CategoryId, out BudgetCategory? cat);
            return new BudgetTransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                CurrencyCode = t.CurrencyCode,
                ReportingAmount = conversion.Convert(t.Amount, t.CurrencyCode, t.Date),
                Description = t.Description,
                CategoryId = t.CategoryId,
                CategoryName = cat?.Name ?? "Unknown",
                Kind = cat?.Kind ?? BudgetCategoryKind.Expense
            };
        }).ToList();

        return new MonthlyBudgetSheetDto
        {
            ReportingCurrency = reportingCurrency,
            Conversion = conversion.ToDto(),
            Year = year,
            Month = month,
            MonthName = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            IsInitialized = isInitialized,
            CanCopyFromPrevious = canCopyFromPrevious,
            PreviousPlanYear = earlierPlanYear,
            PreviousPlanMonth = earlierPlanMonth,
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

    /// <summary>
    /// The stored sum of a category's transactions, in their one shared currency, when that is not the Reporting
    /// Currency; null for a mixed category or one already in the Reporting Currency.
    /// </summary>
    private static (string CurrencyCode, decimal Sum)? OriginalSum(IEnumerable<BudgetTransaction> transactions,
        ReportingConversion conversion)
    {
        List<BudgetTransaction> list = transactions.ToList();
        string currency = list[0].CurrencyCode;
        if (conversion.IsReporting(currency) || list.Any(t => !string.Equals(t.CurrencyCode, currency, StringComparison.Ordinal)))
        {
            return null;
        }

        return (currency, list.Sum(t => t.Amount));
    }

    /// <summary>
    /// A sheet row: the calculator's figures in the Reporting Currency, plus the stored amounts and currency of a line
    /// (or a single-currency transaction sum) that is in another one, so the page can show both.
    /// </summary>
    private static SheetRowDto ToRow(CalculatedBudgetCategoryRow row, Dictionary<int, MonthlyBudgetLine> storedLines,
        Dictionary<int, (string CurrencyCode, decimal Sum)?> txOriginalByCategory, ReportingConversion conversion)
    {
        storedLines.TryGetValue(row.CategoryId, out MonthlyBudgetLine? line);
        bool foreignLine = line is not null && !conversion.IsReporting(line.CurrencyCode);
        (string CurrencyCode, decimal Sum)? transactions = txOriginalByCategory.GetValueOrDefault(row.CategoryId);
        string currencyCode = foreignLine
            ? line!.CurrencyCode
            : !row.ActualIsOverride && transactions is { } sum ? sum.CurrencyCode : conversion.ReportingCurrency;

        return new SheetRowDto
        {
            CategoryId = row.CategoryId,
            Name = row.Name,
            Planned = row.Planned,
            Actual = row.Actual,
            Diff = row.Diff,
            ActualIsOverride = row.ActualIsOverride,
            IsArchived = row.IsArchived,
            CurrencyCode = currencyCode,
            OriginalPlanned = foreignLine ? line!.PlannedAmount : null,
            OriginalActual = row.ActualIsOverride ? (foreignLine ? line!.ActualOverride : null) : transactions?.Sum
        };
    }

    /// <summary>The transaction as the page shows it: its own amount and currency, plus the reporting amount at its day.</summary>
    private async Task<BudgetTransactionDto> ToDtoAsync(string userId, BudgetTransaction transaction,
        BudgetCategory category, CancellationToken ct)
    {
        string reportingCurrency = await _currencyConverter.GetReportingCurrencyAsync(userId, ct);
        ReportingConversion conversion = await _currencyConverter.PrepareAsync(reportingCurrency,
            [new MoneyAt(transaction.CurrencyCode, transaction.Date)], ct);
        return new BudgetTransactionDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Amount = transaction.Amount,
            CurrencyCode = transaction.CurrencyCode,
            ReportingAmount = conversion.Convert(transaction.Amount, transaction.CurrencyCode, transaction.Date),
            Description = transaction.Description,
            CategoryId = transaction.CategoryId,
            CategoryName = category.Name,
            Kind = category.Kind
        };
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetSheetDto> InitializeMonthAsync(string userId, InitializeMonthRequest request,
        CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesAsync(userId, ct);

        MonthlyBudgetPlan? existingPlan = await _dbContext.MonthlyBudgetPlans
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
                MonthlyBudgetPlan? earlierPlan = await _dbContext.MonthlyBudgetPlans
                    .Include(p => p.Lines)
                    .Where(p => p.UserId == userId &&
                                (p.Year < request.Year || (p.Year == request.Year && p.Month < request.Month)))
                    .OrderByDescending(p => p.Year)
                    .ThenByDescending(p => p.Month)
                    .FirstOrDefaultAsync(ct);

                if (earlierPlan != null)
                {
                    // Copy ONLY lines for active (non-archived) categories belonging to this user
                    HashSet<int> activeCategoryIds = await _dbContext.BudgetCategories
                        .Where(c => c.UserId == userId)
                        .Select(c => c.Id)
                        .ToHashSetAsync(ct);

                    foreach (MonthlyBudgetLine? line in earlierPlan.Lines.Where(l => activeCategoryIds.Contains(l.CategoryId)))
                    {
                        plan.Lines.Add(new MonthlyBudgetLine
                        {
                            UserId = userId,
                            CategoryId = line.CategoryId,
                            CurrencyCode = line.CurrencyCode,
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
                foreach (MonthlyBudgetLine line in plan.Lines)
                {
                    EntityEntry<MonthlyBudgetLine> lineEntry = _dbContext.Entry(line);
                    if (lineEntry.State != EntityState.Detached)
                    {
                        lineEntry.State = EntityState.Detached;
                    }
                }

                EntityEntry<MonthlyBudgetPlan> planEntry = _dbContext.Entry(plan);
                if (planEntry.State != EntityState.Detached)
                {
                    planEntry.State = EntityState.Detached;
                }

                MonthlyBudgetPlan? reloadedPlan = await _dbContext.MonthlyBudgetPlans
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
        BudgetCategory? category = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);

        if (category == null)
        {
            throw CategoryNotFound();
        }

        await EnsureArchivedCategoryIsEditableInMonthAsync(category, userId, year, month, ct);

        MonthlyBudgetPlan plan = await GetOrCreatePlanAsync(userId, year, month, ct);

        MonthlyBudgetLine? line = plan.Lines.FirstOrDefault(l => l.CategoryId == categoryId);
        if (line != null)
        {
            line.CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode, line.CurrencyCode);
            line.PlannedAmount = SupportedCurrencies.Round(line.CurrencyCode, request.Amount);
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
                CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode),
                PlannedAmount = SupportedCurrencies.Round(CurrencyCodeRules.Resolve(request.CurrencyCode), request.Amount),
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
                EntityEntry<MonthlyBudgetLine> lineEntry = _dbContext.Entry(newLine);
                if (lineEntry.State != EntityState.Detached)
                {
                    lineEntry.State = EntityState.Detached;
                }

                plan.Lines.Remove(newLine);

                MonthlyBudgetLine? existingLine = await _dbContext.MonthlyBudgetLines
                    .FirstOrDefaultAsync(l => l.PlanId == plan.Id && l.CategoryId == categoryId && l.UserId == userId,
                        ct);

                if (existingLine != null)
                {
                    existingLine.CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode, existingLine.CurrencyCode);
                    existingLine.PlannedAmount = SupportedCurrencies.Round(existingLine.CurrencyCode, request.Amount);
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
        BudgetCategory? category = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);

        if (category == null)
        {
            throw CategoryNotFound();
        }

        await EnsureArchivedCategoryIsEditableInMonthAsync(category, userId, year, month, ct);

        MonthlyBudgetPlan plan = await GetOrCreatePlanAsync(userId, year, month, ct);

        MonthlyBudgetLine? line = plan.Lines.FirstOrDefault(l => l.CategoryId == categoryId);
        if (line != null)
        {
            line.CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode, line.CurrencyCode);
            line.ActualOverride = request.Amount.HasValue
                ? SupportedCurrencies.Round(line.CurrencyCode, request.Amount.Value)
                : null;
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
                CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode),
                PlannedAmount = 0m,
                ActualOverride = request.Amount.HasValue
                    ? SupportedCurrencies.Round(CurrencyCodeRules.Resolve(request.CurrencyCode), request.Amount.Value)
                    : null
            };
            plan.Lines.Add(newLine);
            _dbContext.MonthlyBudgetLines.Add(newLine);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                EntityEntry<MonthlyBudgetLine> lineEntry = _dbContext.Entry(newLine);
                if (lineEntry.State != EntityState.Detached)
                {
                    lineEntry.State = EntityState.Detached;
                }

                plan.Lines.Remove(newLine);

                MonthlyBudgetLine? existingLine = await _dbContext.MonthlyBudgetLines
                    .FirstOrDefaultAsync(l => l.PlanId == plan.Id && l.CategoryId == categoryId && l.UserId == userId,
                        ct);

                if (existingLine != null)
                {
                    existingLine.CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode, existingLine.CurrencyCode);
                    existingLine.ActualOverride = request.Amount.HasValue
                        ? SupportedCurrencies.Round(existingLine.CurrencyCode, request.Amount.Value)
                        : null;
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

        IQueryable<BudgetCategory> query = _dbContext.BudgetCategories.AsQueryable();

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

        string name = request.Name.Trim();
        bool duplicate = await _dbContext.BudgetCategories
            .Where(c => c.UserId == userId && c.Kind == request.Kind)
            .AnyAsync(NameEqualsIgnoreCase(name), ct);

        if (duplicate)
        {
            throw DuplicateCategory(name);
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
            EntityEntry<BudgetCategory> entry = _dbContext.Entry(category);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }

            bool existing = await _dbContext.BudgetCategories
                .Where(c => c.UserId == userId && c.Kind == request.Kind)
                .AnyAsync(NameEqualsIgnoreCase(name), ct);

            if (existing)
            {
                throw DuplicateCategory(name);
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
        BudgetCategory? category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (category == null)
        {
            throw CategoryNotFound();
        }

        if (request.Kind != category.Kind)
        {
            throw new ApiErrorException(ApiErrorKeys.BudgetTracker.CategoryKindImmutable,
                "Category kind cannot be changed.", nameof(SaveCategoryRequest.Kind));
        }

        string name = request.Name.Trim();
        bool duplicate = await _dbContext.BudgetCategories
            .Where(c => c.Id != id && c.UserId == userId && c.Kind == category.Kind)
            .AnyAsync(NameEqualsIgnoreCase(name), ct);

        if (duplicate)
        {
            throw DuplicateCategory(name);
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
        BudgetCategory? category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (category == null)
        {
            throw CategoryNotFound();
        }

        category.SoftDelete();

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BudgetTransactionDto> CreateTransactionAsync(string userId, SaveTransactionRequest request,
        CancellationToken ct = default)
    {
        BudgetCategory? category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

        if (category == null)
        {
            BudgetCategory? archivedCat = await _dbContext.BudgetCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

            if (archivedCat != null)
            {
                throw ArchivedCategory();
            }

            throw CategoryNotFound();
        }

        var transaction = new BudgetTransaction
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Date = request.Date,
            CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode),
            Amount = SupportedCurrencies.Round(CurrencyCodeRules.Resolve(request.CurrencyCode), request.Amount),
            Description = request.Description?.Trim() ?? string.Empty
        };

        _dbContext.BudgetTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(ct);

        return await ToDtoAsync(userId, transaction, category, ct);
    }

    /// <inheritdoc />
    public async Task<BudgetTransactionDto> UpdateTransactionAsync(string userId, int id,
        SaveTransactionRequest request, CancellationToken ct = default)
    {
        BudgetTransaction? transaction = await _dbContext.BudgetTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        if (transaction == null)
        {
            throw TransactionNotFound();
        }

        BudgetCategory? category = await _dbContext.BudgetCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

        if (category == null)
        {
            BudgetCategory? archivedCat = await _dbContext.BudgetCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, ct);

            if (archivedCat != null)
            {
                throw ArchivedCategory();
            }

            throw CategoryNotFound();
        }

        transaction.CategoryId = request.CategoryId;
        transaction.Date = request.Date;
        transaction.CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode, transaction.CurrencyCode);
        transaction.Amount = SupportedCurrencies.Round(transaction.CurrencyCode, request.Amount);
        transaction.Description = request.Description?.Trim() ?? string.Empty;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await ToDtoAsync(userId, transaction, category, ct);
    }

    /// <inheritdoc />
    public async Task DeleteTransactionAsync(string userId, int id, CancellationToken ct = default)
    {
        BudgetTransaction? transaction = await _dbContext.BudgetTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        if (transaction == null)
        {
            throw TransactionNotFound();
        }

        transaction.SoftDelete();

        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<MonthlyBudgetPlan> GetOrCreatePlanAsync(string userId, int year, int month, CancellationToken ct)
    {
        MonthlyBudgetPlan? plan = await _dbContext.MonthlyBudgetPlans
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
            EntityEntry<MonthlyBudgetPlan> entry = _dbContext.Entry(plan);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }

            MonthlyBudgetPlan? existingPlan = await _dbContext.MonthlyBudgetPlans
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
        DateOnly endDate = startDate.AddMonths(1);

        int? existingPlanId = await _dbContext.MonthlyBudgetPlans
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Year == year && p.Month == month)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

        bool hasLineUsage = existingPlanId.HasValue
                           && await _dbContext.MonthlyBudgetLines
                               .AsNoTracking()
                               .AnyAsync(l => l.PlanId == existingPlanId.Value
                                              && l.CategoryId == category.Id
                                              && (l.PlannedAmount > 0 || l.ActualOverride.HasValue), ct);

        if (hasLineUsage)
        {
            return;
        }

        bool hasTransactions = await _dbContext.BudgetTransactions
            .AnyAsync(t => t.UserId == userId
                           && t.CategoryId == category.Id
                           && t.Date >= startDate
                           && t.Date < endDate, ct);

        if (!hasTransactions)
        {
            throw CategoryNotFound();
        }
    }

    /// <summary>The 404 for a category that does not exist, is another user's, or is archived where that is not allowed.</summary>
    private static ApiErrorException CategoryNotFound() =>
        new(ApiErrorKeys.BudgetTracker.CategoryNotFound, "Category not found.", statusCode: StatusCodes.Status404NotFound);

    /// <summary>The 404 for a transaction that does not exist or is another user's.</summary>
    private static ApiErrorException TransactionNotFound() =>
        new(ApiErrorKeys.BudgetTracker.TransactionNotFound, "Transaction not found.", statusCode: StatusCodes.Status404NotFound);

    /// <summary>The 409 for a category whose name another category of the same kind already has.</summary>
    private static ApiErrorException DuplicateCategory(string name) =>
        new(ApiErrorKeys.BudgetTracker.DuplicateCategory, $"A category named \"{name}\" already exists.",
            nameof(SaveCategoryRequest.Name), ErrorArgs.Text("name", name), StatusCodes.Status409Conflict);

    /// <summary>The 400 for a transaction given to an archived category.</summary>
    private static ApiErrorException ArchivedCategory() =>
        new(ApiErrorKeys.BudgetTracker.ArchivedCategory, "Transactions cannot be added to an archived category.",
            nameof(SaveTransactionRequest.CategoryId));

    /// <summary>
    /// Matches a category whose name equals <paramref name="name"/>, ignoring case. Used by the duplicate-category
    /// checks; EF translates it to <c>LOWER([Name]) = @name</c>, sending the argument already lowered.
    /// </summary>
    internal static Expression<Func<BudgetCategory, bool>> NameEqualsIgnoreCase(string name)
    {
        // CA1304/CA1311/CA1862: the column is lowered by SQL Server (EF translates ToLower() to LOWER()), and the
        // argument is lowered by EF before it becomes a parameter, under the app culture each host pins at startup.
        // The suggested ToLowerInvariant() and string.Equals(StringComparison) overloads don't translate to SQL.
#pragma warning disable CA1304, CA1311, CA1862
        return c => c.Name.ToLower() == name.ToLower();
#pragma warning restore CA1304, CA1311, CA1862
    }
}
