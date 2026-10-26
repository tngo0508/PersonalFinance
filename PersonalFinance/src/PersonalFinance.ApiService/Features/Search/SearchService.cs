using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Search;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Search;

/// <summary>
/// Global Search across transactions, Budget Categories, and Savings Goals. Transactions and categories come only
/// from the user's active Data Source (Google Sheet or Budget Tracker), so results never mix the two.
/// </summary>
internal sealed class SearchService : ISearchService
{
    /// <summary>Queries shorter than this (after trimming) return no results.</summary>
    internal const int MinQueryLength = 2;

    /// <summary>Longer queries are cut to this length before searching.</summary>
    internal const int MaxQueryLength = 100;

    internal const int MaxTransactions = 10;
    internal const int MaxCategories = 5;
    internal const int MaxSavingsGoals = 5;

    private const string LikeEscape = "\\";

    private readonly AppDbContext _dbContext;
    private readonly IBudgetDataSourceService _dataSourceService;
    private readonly TimeProvider _timeProvider;

    public SearchService(AppDbContext dbContext, IBudgetDataSourceService dataSourceService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _dataSourceService = dataSourceService;
        _timeProvider = timeProvider;
    }

    public async Task<SearchResultsDto> SearchAsync(string userId, string query,
        CancellationToken cancellationToken = default)
    {
        string? pattern = ToContainsPattern(query);
        if (pattern is null)
        {
            return new SearchResultsDto();
        }

        BudgetDataSource source = await _dataSourceService.GetActiveSourceAsync(userId, cancellationToken);
        bool isTracker = source == BudgetDataSource.BudgetTracker;
        IQueryable<SearchResultItemDto> transactionsQuery = isTracker
            ? TrackerTransactionsQuery(userId, pattern)
            : SheetTransactionsQuery(userId, pattern);
        IQueryable<SearchResultItemDto> categoriesQuery = isTracker
            ? TrackerCategoriesQuery(userId, pattern)
            : SheetCategoriesQuery(userId, pattern);

        // One DbContext does not support concurrent operations, so the groups are read one after another.
        return new SearchResultsDto
        {
            Transactions = await transactionsQuery.ToListAsync(cancellationToken),
            Categories = await categoriesQuery.ToListAsync(cancellationToken),
            SavingsGoals = await SavingsGoalsQuery(userId, pattern).ToListAsync(cancellationToken)
        };
    }

    /// <summary>
    /// Builds a case-insensitive LIKE "contains" pattern from the user's query, with LIKE wildcards escaped so they
    /// match literally. Returns null when the query is too short to search.
    /// </summary>
    internal static string? ToContainsPattern(string? query)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length < MinQueryLength)
        {
            return null;
        }

        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed[..MaxQueryLength];
        }

        string escaped = trimmed
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
                    $"{t.Date:MMM d, yyyy} - {t.Category!.Name} - {MoneyFormatter.Format(t.Amount, SupportedCurrencies.Default.Code)} ({t.Category!.Kind})",
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
                Description = $"{t.Date:MMM d, yyyy} - {t.Category} - {MoneyFormatter.Format(t.Amount, SupportedCurrencies.Default.Code)} ({t.Type})",
                Source = "Transaction",
                Url = $"/Analysis?year={t.Date.Year}&month={t.Date.Month}",
                Date = t.Date,
                Amount = t.Amount
            });
    }

    /// <summary>
    /// Budget Categories by name. Deleted categories are left out (the soft-delete filter applies). The link opens
    /// the current month in the Budget Tracker.
    /// </summary>
    internal IQueryable<SearchResultItemDto> TrackerCategoriesQuery(string userId, string pattern)
    {
        string url = $"/BudgetTracker?{CurrentMonthQuery()}";
        return _dbContext.BudgetCategories
            .AsNoTracking()
            .Where(c => c.UserId == userId && EF.Functions.Like(c.Name, pattern, LikeEscape))
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Take(MaxCategories)
            .Select(c => new SearchResultItemDto
            {
                Id = c.Id.ToString(CultureInfo.InvariantCulture),
                Title = c.Name,
                Description = $"{c.Kind} category",
                Source = "Category",
                Url = url
            });
    }

    /// <summary>
    /// The distinct category names on synced Google Sheet transactions. The link opens the current month in
    /// Analysis.
    /// </summary>
    internal IQueryable<SearchResultItemDto> SheetCategoriesQuery(string userId, string pattern)
    {
        string url = $"/Analysis?{CurrentMonthQuery()}";
        return _dbContext.SpreadsheetTransactionRecords
            .AsNoTracking()
            .Where(t => t.UserId == userId && EF.Functions.Like(t.Category, pattern, LikeEscape))
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(name => name)
            .Take(MaxCategories)
            .Select(name => new SearchResultItemDto
            {
                Id = name,
                Title = name,
                Description = "Google Sheet category",
                Source = "Category",
                Url = url
            });
    }

    /// <summary>The current UTC month as a query string, as the Budget Tracker page defaults to it.</summary>
    private string CurrentMonthQuery()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return string.Create(CultureInfo.InvariantCulture, $"year={now.Year}&month={now.Month}");
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
                Description = g.Description ?? $"Target: {MoneyFormatter.Format(g.TargetAmount, SupportedCurrencies.Default.Code)}",
                Source = "SavingsGoal",
                Url = "/SavingsGoals"
            });
    }
}
