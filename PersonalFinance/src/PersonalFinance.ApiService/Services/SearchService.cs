using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service for unified search across transactions, budgets, and savings goals.
/// </summary>
public class SearchService : ISearchService
{
    private readonly AppDbContext _dbContext;

    public SearchService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SearchResultsDto> SearchAsync(string userId, string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResultsDto();
        }

        var searchQuery = query.Trim().ToLowerInvariant();

        var transactionsTask = SearchTransactionsAsync(userId, searchQuery, cancellationToken);
        var budgetsTask = SearchBudgetsAsync(userId, searchQuery, cancellationToken);
        var savingsGoalsTask = SearchSavingsGoalsAsync(userId, searchQuery, cancellationToken);

        await Task.WhenAll(transactionsTask, budgetsTask, savingsGoalsTask);

        return new SearchResultsDto
        {
            Transactions = await transactionsTask,
            BudgetItems = await budgetsTask,
            SavingsGoals = await savingsGoalsTask
        };
    }

    private async Task<List<SearchResultDto>> SearchTransactionsAsync(string userId, string query,
        CancellationToken cancellationToken)
    {
        var transactions = await _dbContext.SpreadsheetTransactionRecords
            .Where(t => t.UserId == userId &&
                        (t.Description.ToLowerInvariant().Contains(query) ||
                         t.Category.ToLowerInvariant().Contains(query)))
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new SearchResultDto
            {
                Id = t.Id.ToString(),
                Title = t.Description,
                Description = $"{t.Category} - {t.Amount:C} ({t.Type})",
                Source = "Transaction",
                Url = $"/Analysis?transactionId={t.Id}"
            })
            .ToListAsync(cancellationToken);

        return transactions;
    }

    private async Task<List<SearchResultDto>> SearchBudgetsAsync(string userId, string query,
        CancellationToken cancellationToken)
    {
        var budgets = await _dbContext.UserBudgetAllocations
            .Where(b => b.UserId == userId &&
                        b.Period.ToLowerInvariant().Contains(query))
            .OrderByDescending(b => b.Period)
            .Take(5)
            .Select(b => new SearchResultDto
            {
                Id = b.Id.ToString(),
                Title = $"Budget for {b.Period}",
                Description = $"Monthly income: {b.MonthlyIncome:C}",
                Source = "Budget",
                Url = $"/Budget?period={b.Period}"
            })
            .ToListAsync(cancellationToken);

        return budgets;
    }

    private async Task<List<SearchResultDto>> SearchSavingsGoalsAsync(string userId, string query,
        CancellationToken cancellationToken)
    {
        var goals = await _dbContext.SavingsGoals
            .Where(g => g.UserId == userId &&
                        (g.Name.ToLowerInvariant().Contains(query) ||
                         (!string.IsNullOrEmpty(g.Description) && g.Description.ToLowerInvariant().Contains(query))))
            .OrderByDescending(g => g.CreatedAt)
            .Take(10)
            .Select(g => new SearchResultDto
            {
                Id = g.Id.ToString(),
                Title = g.Name,
                Description = g.Description ?? $"Target: {g.TargetAmount:C}",
                Source = "SavingsGoal",
                Url = $"/SavingsGoals"
            })
            .ToListAsync(cancellationToken);

        return goals;
    }
}
