using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Search;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Global Search across transactions, budgets, and savings goals. Transactions come only from the user's active Data
/// Source (Google Sheet or Budget Tracker), so results never mix the two.
/// </summary>
public class SearchService : ISearchService
{
    /// <summary>Queries shorter than this (after trimming) return no results.</summary>
    public const int MinQueryLength = 2;

    /// <summary>Longer queries are cut to this length before searching.</summary>
    public const int MaxQueryLength = 100;

    internal const int MaxTransactions = 10;
    internal const int MaxBudgetItems = 5;
    internal const int MaxSavingsGoals = 5;

    private const string LikeEscape = "\\";

    private readonly AppDbContext _dbContext;
    private readonly IBudgetDataSourceService _dataSourceService;

    public SearchService(AppDbContext dbContext, IBudgetDataSourceService dataSourceService)
    {
        _dbContext = dbContext;
        _dataSourceService = dataSourceService;
    }

    public async Task<SearchResultsDto> SearchAsync(string userId, string query,
        CancellationToken cancellationToken = default)
    {
        var pattern = ToContainsPattern(query);
        if (pattern is null)
        {
            return new SearchResultsDto();
        }

        var source = await _dataSourceService.GetActiveSourceAsync(userId, cancellationToken);
        var transactionsQuery = source == BudgetDataSource.BudgetTracker
            ? TrackerTransactionsQuery(userId, pattern)
            : SheetTransactionsQuery(userId, pattern);

        // One DbContext does not support concurrent operations, so the groups are read one after another.
        return new SearchResultsDto
        {
            Transactions = await transactionsQuery.ToListAsync(cancellationToken),
            BudgetItems = await BudgetItemsQuery(userId, pattern).ToListAsync(cancellationToken),
            SavingsGoals = await SavingsGoalsQuery(userId, pattern).ToListAsync(cancellationToken)
        };
    }

    /// <summary>
    /// Builds a case-insensitive LIKE "contains" pattern from the user's query, with LIKE wildcards escaped so they
    /// match literally. Returns null when the query is too short to search.
    /// </summary>
    internal static string? ToContainsPattern(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length < MinQueryLength)
        {
            return null;
        }

        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed[..MaxQueryLength];
        }

        var escaped = trimmed
            .Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_")
            .Replace("[", LikeEscape + "[");

        return $"%{escaped}%";
    }

    internal IQueryable<SearchResultItemDto> TrackerTransactionsQuery(string userId, string pattern)
    {
        // Filters are ignored so a deleted Budget Category still names the past transactions that used it, as in
        // the Budget Tracker itself; the transaction's own soft delete is checked explicitly.
        return _dbContext.BudgetTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsDeleted &&
                        (EF.Functions.Like(t.Description, pattern, LikeEscape) ||
                         EF.Functions.Like(t.Category!.Name, pattern, LikeEscape)))
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(MaxTransactions)
            .Select(t => new SearchResultItemDto
            {
                Id = t.Id.ToString(CultureInfo.InvariantCulture),
                Title = t.Description,
                Description =
                    $"{t.Date:MMM d, yyyy} - {t.Category!.Name} - {t.Amount:C} ({t.Category!.Kind})",
                Source = "Transaction",
                Url = $"/BudgetTracker?year={t.Date.Year}&month={t.Date.Month}",
                Date = t.Date.ToDateTime(TimeOnly.MinValue),
                Amount = t.Amount
            });
    }

    internal IQueryable<SearchResultItemDto> SheetTransactionsQuery(string userId, string pattern)
    {
        return _dbContext.SpreadsheetTransactionRecords
            .AsNoTracking()
            .Where(t => t.UserId == userId &&
                        (EF.Functions.Like(t.Description, pattern, LikeEscape) ||
                         EF.Functions.Like(t.Category, pattern, LikeEscape)))
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(MaxTransactions)
            .Select(t => new SearchResultItemDto
            {
                Id = t.Id.ToString(CultureInfo.InvariantCulture),
                Title = t.Description,
                Description = $"{t.Date:MMM d, yyyy} - {t.Category} - {t.Amount:C} ({t.Type})",
                Source = "Transaction",
                Url = $"/Analysis?year={t.Date.Year}&month={t.Date.Month}",
                Date = t.Date,
                Amount = t.Amount
            });
    }

    internal IQueryable<SearchResultItemDto> BudgetItemsQuery(string userId, string pattern)
    {
        return _dbContext.UserBudgetAllocations
            .AsNoTracking()
            .Where(b => b.UserId == userId && EF.Functions.Like(b.Period, pattern, LikeEscape))
            .OrderByDescending(b => b.Period)
            .ThenByDescending(b => b.Id)
            .Take(MaxBudgetItems)
            .Select(b => new SearchResultItemDto
            {
                Id = b.Id.ToString(CultureInfo.InvariantCulture),
                Title = $"Budget for {b.Period}",
                Description = $"Monthly income: {b.MonthlyIncome:C}",
                Source = "Budget",
                Url = $"/Budget?period={b.Period}"
            });
    }

    internal IQueryable<SearchResultItemDto> SavingsGoalsQuery(string userId, string pattern)
    {
        return _dbContext.SavingsGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId &&
                        (EF.Functions.Like(g.Name, pattern, LikeEscape) ||
                         (g.Description != null && EF.Functions.Like(g.Description, pattern, LikeEscape))))
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Id)
            .Take(MaxSavingsGoals)
            .Select(g => new SearchResultItemDto
            {
                Id = g.Id.ToString(CultureInfo.InvariantCulture),
                Title = g.Name,
                Description = g.Description ?? $"Target: {g.TargetAmount:C}",
                Source = "SavingsGoal",
                Url = "/SavingsGoals"
            });
    }
}
