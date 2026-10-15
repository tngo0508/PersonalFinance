using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service implementation for interacting with the AI assistant (OpenAI).
/// </summary>
public class AiAssistantService : IAiAssistantService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly IGoogleDriveService _googleDriveService;
    private readonly ILogger<AiAssistantService> _logger;
    private readonly string _modelName;
    private readonly IRateLimiterService _rateLimiterService;

    public AiAssistantService(
        AppDbContext dbContext,
        IGoogleDriveService googleDriveService,
        IRateLimiterService rateLimiterService,
        ILogger<AiAssistantService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _googleDriveService = googleDriveService;
        _rateLimiterService = rateLimiterService;
        _logger = logger;
        _configuration = configuration;
        _modelName = configuration.GetValue<string>("OpenAI:Model") ?? "gpt-4o-mini";
    }

    public async Task<AiChatResponseDto> SendChatMessageAsync(string userId, AiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.GetValue<string>("OpenAI:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured. Returning a fallback response.");
            return new AiChatResponseDto
            {
                Response =
                    "AI assistant is currently unavailable. Please configure the OpenAI API key to enable this feature.",
                Model = _modelName,
                TimestampUtc = DateTime.UtcNow
            };
        }

        try
        {
            // Gather user's financial context
            var financialContext = await GetUserFinancialContextAsync(userId, cancellationToken);

            // Build the system prompt
            var systemPrompt = BuildSystemPrompt(financialContext);

            // Create the OpenAI client
            var client = new OpenAIClient(apiKey);
            var chatClient = client.GetChatClient(_modelName);

            // Build the messages list
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt)
            };

            // Add conversation history if provided
            if (request.ConversationHistory != null && request.ConversationHistory.Count > 0)
            {
                // Limit history to last 10 messages to control token usage
                var recentHistory = request.ConversationHistory.TakeLast(10).ToList();
                foreach (var msg in recentHistory)
                {
                    if (msg.Role == "user")
                    {
                        messages.Add(new UserChatMessage(msg.Content));
                    }
                    else if (msg.Role == "assistant")
                    {
                        messages.Add(new AssistantChatMessage(msg.Content));
                    }
                }
            }

            // Add the current user message
            messages.Add(new UserChatMessage(request.Message));

            // Send the request to OpenAI
            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

            var responseText = response.Value.Content[0]?.Text ?? "I'm sorry, I couldn't generate a response.";

            _logger.LogInformation("User {UserId} sent a message to AI assistant. Model: {Model}", userId, _modelName);

            return new AiChatResponseDto
            {
                Response = responseText,
                Model = _modelName,
                TimestampUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to OpenAI for user {UserId}", userId);
            return new AiChatResponseDto
            {
                Response = "I'm sorry, I encountered an error processing your request. Please try again later.",
                Model = _modelName,
                TimestampUtc = DateTime.UtcNow
            };
        }
    }

    public async Task<NewsArticleSummaryResponseDto> SummarizeArticleAsync(string userId,
        NewsArticleSummaryRequestDto request, CancellationToken cancellationToken = default)
    {
        // Check rate limit
        if (!_rateLimiterService.TryAllow(userId))
        {
            _logger.LogWarning("Rate limit exceeded for user {UserId} on summarize endpoint.", userId);
            throw new InvalidOperationException("Rate limit exceeded. Please try again later.");
        }

        var apiKey = _configuration.GetValue<string>("OpenAI:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured. Returning a fallback summary.");
            return new NewsArticleSummaryResponseDto
            {
                Summary =
                    "AI summarization is currently unavailable. Please configure the OpenAI API key to enable this feature.",
                Model = _modelName,
                TimestampUtc = DateTime.UtcNow
            };
        }

        try
        {
            // Truncate description to avoid excessive token usage
            var description = string.IsNullOrEmpty(request.Description)
                ? string.Empty
                : request.Description.Length > 4000
                    ? request.Description.Substring(0, 4000) + "..."
                    : request.Description;

            // Build the system prompt — news only, no financial advice
            var systemPrompt =
                "You are a helpful news summarizer. Summarize the following news article in 2-3 concise sentences in plain English. " +
                "Focus on the key facts and main points. Do not provide financial advice or recommendations.";

            // Create the OpenAI client
            var client = new OpenAIClient(apiKey);
            var chatClient = client.GetChatClient(_modelName);

            // Build the user message with title and description
            var userMessage = $"Title: {request.Title}\n\nDescription: {description}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            // Send the request to OpenAI
            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

            var summaryText = response.Value.Content[0]?.Text ?? "Unable to generate a summary at this time.";

            _logger.LogInformation("User {UserId} summarized a news article. Model: {Model}", userId, _modelName);

            return new NewsArticleSummaryResponseDto
            {
                Summary = summaryText,
                Model = _modelName,
                TimestampUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error summarizing news article for user {UserId}", userId);
            return new NewsArticleSummaryResponseDto
            {
                Summary = "I'm sorry, I encountered an error generating the summary. Please try again later.",
                Model = _modelName,
                TimestampUtc = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Gets the user's financial context from the database.
    /// </summary>
    private async Task<UserFinancialContext> GetUserFinancialContextAsync(string userId,
        CancellationToken cancellationToken)
    {
        var context = new UserFinancialContext();

        try
        {
            // Get net worth parameters
            var netWorthParams = await _dbContext.NetWorthParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.UserId == userId, cancellationToken);

            if (netWorthParams != null)
            {
                context.AnnualIncome = netWorthParams.AnnualIncome;
                context.AnnualSpending = netWorthParams.AnnualSpending;
                context.CurrentNetWorth = netWorthParams.StartingBalance;
                context.CurrentAge = netWorthParams.CurrentAge;
                context.RetirementAge = netWorthParams.RetirementAge;
                context.AnnualSavings = netWorthParams.AnnualSavings;
            }

            // Get budget allocations
            var budget = await _dbContext.UserBudgetAllocations
                .AsNoTracking()
                .OrderByDescending(b => b.UpdatedAtUtc)
                .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

            if (budget != null)
            {
                context.MonthlyIncome = budget.MonthlyIncome;
                context.BudgetAllocations = new Dictionary<string, double>
                {
                    { "Housing", (double)budget.HousingPercent },
                    { "Food", (double)budget.FoodPercent },
                    { "Transportation", (double)budget.TransportationPercent },
                    { "Utilities", (double)budget.UtilitiesPercent },
                    { "Insurance", (double)budget.InsurancePercent },
                    { "Healthcare", (double)budget.HealthcarePercent },
                    { "Savings", (double)budget.SavingsPercent },
                    { "Debt Repayment", (double)budget.DebtRepaymentPercent },
                    { "Entertainment", (double)budget.EntertainmentPercent },
                    { "Miscellaneous", (double)budget.MiscellaneousPercent }
                };
            }

            // Get savings goals
            var savingsGoals = await _dbContext.SavingsGoals
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .ToListAsync(cancellationToken);

            if (savingsGoals.Count > 0)
            {
                context.SavingsGoals = savingsGoals.Select(s => new SavingsGoalInfo
                {
                    Name = s.Name,
                    TargetAmount = s.TargetAmount,
                    CurrentAmount = s.CurrentAmount,
                    ProgressPercent = s.TargetAmount > 0 ? (double)(s.CurrentAmount / s.TargetAmount) * 100 : 0,
                    IsCompleted = s.IsCompleted,
                    TargetDate = s.TargetDate
                }).ToList();
            }

            // Get budget report and transaction data from connected Google Drive spreadsheets
            await GetSpreadsheetDataAsync(userId, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving financial context for user {UserId}", userId);
        }

        return context;
    }

    /// <summary>
    ///     Fetches budget report and transaction data from the user's connected Google Drive spreadsheets.
    /// </summary>
    private async Task GetSpreadsheetDataAsync(string userId, UserFinancialContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find the user's latest valid Google Drive connection with a spreadsheet
            var connection = await _dbContext.GoogleDriveConnections
                .AsNoTracking()
                .OrderByDescending(c => c.LastSyncedAtUtc ?? c.CreatedAtUtc)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsValid, cancellationToken);

            if (connection == null) return;

            // Find spreadsheet files in the cached files
            var spreadsheetFiles = await _dbContext.GoogleDriveCachedFiles
                .AsNoTracking()
                .Where(f => f.ConnectionId == connection.Id && !f.IsFolder && !f.IsTrashed
                            && (f.MimeType.Contains("spreadsheet") || f.MimeType.Contains("sheet")
                                                                   || f.Name.EndsWith(".xlsx",
                                                                       StringComparison.OrdinalIgnoreCase)
                                                                   || f.Name.EndsWith(".xls",
                                                                       StringComparison.OrdinalIgnoreCase)
                                                                   || f.Name.EndsWith(".csv",
                                                                       StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(f => f.ModifiedTime ?? f.LastFetchedUtc)
                .ToListAsync(cancellationToken);

            if (spreadsheetFiles.Count == 0) return;

            // Use the most recently modified spreadsheet
            var latestSpreadsheet = spreadsheetFiles[0];
            var fileId = latestSpreadsheet.DriveFileId;
            var fileName = latestSpreadsheet.Name;

            // Fetch budget report
            try
            {
                var budgetReport = await _googleDriveService.GetSpreadsheetBudgetReportAsync(
                    fileId, fileName, connection.Id, userId, cancellationToken);

                if (budgetReport != null && budgetReport.Months.Count > 0)
                {
                    context.BudgetReportSummary = new BudgetReportSummary
                    {
                        FileName = budgetReport.FileName,
                        Year = budgetReport.Year,
                        TotalAnnualIncome = budgetReport.TotalAnnualIncome,
                        TotalAnnualExpenses = budgetReport.TotalAnnualExpenses,
                        TotalAnnualSavings = budgetReport.TotalAnnualSavings,
                        AverageSavingsRate = budgetReport.AverageSavingsRate,
                        MonthCount = budgetReport.Months.Count,
                        OverBudgetMonths = budgetReport.Months.Count(m => m.Status == "Over Budget"),
                        UnderBudgetMonths = budgetReport.Months.Count(m => m.Status == "Under Budget"),
                        HighestExpenseMonth = budgetReport.Months.OrderByDescending(m => m.ActualExpenses)
                            .FirstOrDefault()?.MonthName,
                        HighestExpenseAmount = budgetReport.Months.OrderByDescending(m => m.ActualExpenses)
                            .FirstOrDefault()?.ActualExpenses,
                        LowestExpenseMonth = budgetReport.Months.OrderBy(m => m.ActualExpenses).FirstOrDefault()
                            ?.MonthName,
                        LowestExpenseAmount = budgetReport.Months.OrderBy(m => m.ActualExpenses).FirstOrDefault()
                            ?.ActualExpenses
                    };

                    // Store detailed monthly budget data for month-specific queries
                    context.MonthlyBudgetDetails = budgetReport.Months.Select(m => new MonthlyBudgetDetail
                    {
                        MonthName = m.MonthName,
                        MonthNumber = m.MonthNumber,
                        BudgetedIncome = m.BudgetedIncome,
                        ActualIncome = m.ActualIncome,
                        BudgetedExpenses = m.BudgetedExpenses,
                        ActualExpenses = m.ActualExpenses,
                        NetSavings = m.NetSavings,
                        SavingsRate = m.SavingsRate,
                        ExpenseVariance = m.ExpenseVariance,
                        Status = m.Status,
                        Categories = m.Categories.Select(c => new BudgetCategoryDetail
                        {
                            CategoryName = c.CategoryName,
                            BudgetedAmount = c.BudgetedAmount,
                            ActualAmount = c.ActualAmount,
                            Variance = c.Variance,
                            PercentageUsed = c.PercentageUsed,
                            Status = c.Status
                        }).ToList()
                    }).ToList();

                    // Get top spending categories across all months
                    var categoryTotals = new Dictionary<string, decimal>();
                    foreach (var month in budgetReport.Months)
                    foreach (var category in month.Categories)
                    {
                        if (!categoryTotals.ContainsKey(category.CategoryName))
                            categoryTotals[category.CategoryName] = 0;

                        categoryTotals[category.CategoryName] += category.ActualAmount;
                    }

                    context.TopSpendingCategories = categoryTotals
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(5)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    // Get categories that are over budget
                    var overBudgetCategories = new List<string>();
                    foreach (var month in budgetReport.Months)
                    foreach (var category in month.Categories)
                        if (category.Status == "Over Budget" &&
                            !overBudgetCategories.Contains(category.CategoryName))
                            overBudgetCategories.Add(category.CategoryName);

                    context.OverBudgetCategories = overBudgetCategories;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch budget report for user {UserId} from file {FileId}", userId,
                    fileId);
            }

            // Fetch transactions
            try
            {
                var transactions = await _googleDriveService.GetSpreadsheetTransactionsAsync(
                    fileId, fileName, connection.Id, userId, cancellationToken);

                if (transactions != null && transactions.Transactions.Count > 0)
                {
                    context.TransactionSummary = new TransactionSummary
                    {
                        FileName = transactions.FileName,
                        TransactionCount = transactions.Transactions.Count,
                        TotalExpenses = transactions.TotalExpenses,
                        TotalIncome = transactions.TotalIncome,
                        FirstDate = transactions.FirstDate,
                        LastDate = transactions.LastDate,
                        AverageTransactionAmount = transactions.Transactions.Count > 0
                            ? transactions.Transactions.Average(t => t.Amount)
                            : 0m
                    };

                    // Get top transaction categories by total amount
                    var transactionCategoryTotals = transactions.Transactions
                        .Where(t => t.Type == SpreadsheetTransactionDto.ExpenseType)
                        .GroupBy(t => t.Category)
                        .OrderByDescending(g => g.Sum(t => t.Amount))
                        .Take(5)
                        .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

                    context.TopTransactionCategories = transactionCategoryTotals;

                    // Get recent large transactions (top 5 by amount)
                    var largeTransactions = transactions.Transactions
                        .OrderByDescending(t => t.Amount)
                        .Take(5)
                        .Select(t => new LargeTransactionInfo
                        {
                            Date = t.Date?.ToString("yyyy-MM-dd") ?? t.DateText,
                            Amount = t.Amount,
                            Description = t.Description,
                            Category = t.Category
                        }).ToList();

                    context.LargeTransactions = largeTransactions;

                    // Store all transactions for detailed month-specific queries
                    context.AllTransactions = transactions.Transactions
                        .Select(t => new LargeTransactionInfo
                        {
                            Date = t.Date?.ToString("yyyy-MM-dd") ?? t.DateText,
                            Amount = t.Amount,
                            Description = t.Description,
                            Category = t.Category
                        }).ToList();

                    // Count transactions by type
                    context.IncomeTransactionCount =
                        transactions.Transactions.Count(t => t.Type == SpreadsheetTransactionDto.IncomeType);
                    context.ExpenseTransactionCount =
                        transactions.Transactions.Count(t => t.Type == SpreadsheetTransactionDto.ExpenseType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch transactions for user {UserId} from file {FileId}", userId,
                    fileId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve spreadsheet data for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Builds the system prompt for the AI assistant.
    /// </summary>
    private string BuildSystemPrompt(UserFinancialContext context)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are a helpful and friendly personal finance assistant for the PersonalFinance app.");
        prompt.AppendLine("You have access to the user's financial data and can provide insights on:");
        prompt.AppendLine("- Net worth projections and retirement planning");
        prompt.AppendLine("- Budget allocation recommendations");
        prompt.AppendLine("- Budget report analysis including monthly variances and category breakdowns");
        prompt.AppendLine("- Transaction report analysis including spending patterns and large transactions");
        prompt.AppendLine("- Savings goal progress");
        prompt.AppendLine("- General financial literacy and advice");
        prompt.AppendLine();
        prompt.AppendLine("IMPORTANT RULES:");
        prompt.AppendLine("1. Always be encouraging and positive, but honest about financial situations.");
        prompt.AppendLine("2. Provide actionable, specific advice based on the user's data.");
        prompt.AppendLine(
            "3. Clarify that you're an AI assistant and recommendations should be verified with a qualified financial advisor.");
        prompt.AppendLine("4. Keep responses concise and easy to understand - avoid overly technical jargon.");
        prompt.AppendLine("5. Use bullet points and formatting to make responses readable.");
        prompt.AppendLine("6. Never make up financial data - only use the data provided in the context below.");
        prompt.AppendLine(
            "7. When analyzing budget reports, highlight over-budget categories and suggest specific reduction strategies.");
        prompt.AppendLine(
            "8. When analyzing transactions, identify spending trends, unusual patterns, and opportunities to save.");
        prompt.AppendLine();

        if (context.HasAnyData)
        {
            prompt.AppendLine("User's current financial context:");

            if (context.AnnualIncome > 0)
            {
                prompt.AppendLine($"- Annual income: ${context.AnnualIncome:N0}");
            }

            if (context.AnnualSpending > 0)
            {
                prompt.AppendLine($"- Annual spending: ${context.AnnualSpending:N0}");
            }

            if (context.CurrentNetWorth > 0)
            {
                prompt.AppendLine($"- Current net worth: ${context.CurrentNetWorth:N0}");
            }

            if (context.AnnualSavings > 0)
            {
                prompt.AppendLine($"- Annual savings: ${context.AnnualSavings:N0}");
            }

            if (context.CurrentAge > 0 && context.RetirementAge > 0)
            {
                prompt.AppendLine($"- Age: {context.CurrentAge}, Retirement target: {context.RetirementAge}");
                var yearsToRetirement = context.RetirementAge - context.CurrentAge;
                if (yearsToRetirement > 0)
                {
                    prompt.AppendLine($"- Years until retirement: {yearsToRetirement}");
                }
            }

            if (context.MonthlyIncome > 0)
            {
                prompt.AppendLine($"- Monthly income: ${context.MonthlyIncome:N0}");
            }

            if (context.BudgetAllocations != null && context.BudgetAllocations.Count > 0)
            {
                prompt.AppendLine("- Budget allocations:");
                foreach (var (category, percent) in context.BudgetAllocations)
                {
                    prompt.AppendLine($"  * {category}: {percent:F1}%");
                }
            }

            if (context.SavingsGoals != null && context.SavingsGoals.Count > 0)
            {
                prompt.AppendLine("- Savings goals:");
                foreach (var goal in context.SavingsGoals)
                {
                    var status = goal.IsCompleted ? "(Completed)" : $"({goal.ProgressPercent:F1}% complete)";
                    prompt.AppendLine($"  * {goal.Name}: ${goal.CurrentAmount:N0} / ${goal.TargetAmount:N0} {status}");
                    if (goal.TargetDate.HasValue)
                    {
                        prompt.AppendLine($"    Target date: {goal.TargetDate.Value:yyyy-MM-dd}");
                    }
                }
            }

            // Budget report summary
            if (context.BudgetReportSummary != null)
            {
                var br = context.BudgetReportSummary;
                prompt.AppendLine($"- Budget report ({br.FileName}, {br.Year}):");
                prompt.AppendLine($"  * Total annual income: ${br.TotalAnnualIncome:N0}");
                prompt.AppendLine($"  * Total annual expenses: ${br.TotalAnnualExpenses:N0}");
                prompt.AppendLine($"  * Total annual savings: ${br.TotalAnnualSavings:N0}");
                prompt.AppendLine($"  * Average savings rate: {br.AverageSavingsRate:F1}%");
                prompt.AppendLine($"  * Months tracked: {br.MonthCount}");
                prompt.AppendLine($"  * Over budget months: {br.OverBudgetMonths}");
                prompt.AppendLine($"  * Under budget months: {br.UnderBudgetMonths}");
                if (br.HighestExpenseMonth != null)
                    prompt.AppendLine(
                        $"  * Highest expense month: {br.HighestExpenseMonth} (${br.HighestExpenseAmount:N0})");

                if (br.LowestExpenseMonth != null)
                    prompt.AppendLine(
                        $"  * Lowest expense month: {br.LowestExpenseMonth} (${br.LowestExpenseAmount:N0})");
            }

            // Top spending categories from budget report
            if (context.TopSpendingCategories != null && context.TopSpendingCategories.Count > 0)
            {
                prompt.AppendLine("- Top spending categories (from budget report):");
                foreach (var (category, amount) in context.TopSpendingCategories)
                    prompt.AppendLine($"  * {category}: ${amount:N0}");
            }

            // Over budget categories
            if (context.OverBudgetCategories != null && context.OverBudgetCategories.Count > 0)
            {
                prompt.AppendLine("- Categories that went over budget:");
                foreach (var category in context.OverBudgetCategories) prompt.AppendLine($"  * {category}");
            }

            // Detailed monthly budget data for month-specific queries
            if (context.MonthlyBudgetDetails != null && context.MonthlyBudgetDetails.Count > 0)
            {
                prompt.AppendLine("- Detailed monthly budget breakdown:");
                foreach (var month in context.MonthlyBudgetDetails)
                {
                    prompt.AppendLine($"  {month.MonthName} (Month {month.MonthNumber}):");
                    prompt.AppendLine($"    Status: {month.Status}");
                    prompt.AppendLine(
                        $"    Budgeted income: ${month.BudgetedIncome:N0}, Actual income: ${month.ActualIncome:N0}");
                    prompt.AppendLine(
                        $"    Budgeted expenses: ${month.BudgetedExpenses:N0}, Actual expenses: ${month.ActualExpenses:N0}");
                    prompt.AppendLine(
                        $"    Net savings: ${month.NetSavings:N0}, Savings rate: {month.SavingsRate:F1}%");
                    prompt.AppendLine($"    Expense variance: ${month.ExpenseVariance:N0}");
                    if (month.Categories.Count > 0)
                    {
                        prompt.AppendLine("    Categories:");
                        foreach (var cat in month.Categories)
                            prompt.AppendLine(
                                $"      - {cat.CategoryName}: Budgeted ${cat.BudgetedAmount:N0}, Actual ${cat.ActualAmount:N0}, Variance ${cat.Variance:N0} ({cat.PercentageUsed:F1}% used, {cat.Status})");
                    }
                }
            }

            // Transaction summary
            if (context.TransactionSummary != null)
            {
                var ts = context.TransactionSummary;
                prompt.AppendLine($"- Transaction report ({ts.FileName}):");
                prompt.AppendLine($"  * Total transactions: {ts.TransactionCount}");
                prompt.AppendLine($"  * Total income: ${ts.TotalIncome:N0}");
                prompt.AppendLine($"  * Total expenses: ${ts.TotalExpenses:N0}");
                prompt.AppendLine($"  * Average transaction: ${ts.AverageTransactionAmount:N0}");
                if (ts.FirstDate.HasValue && ts.LastDate.HasValue)
                    prompt.AppendLine(
                        $"  * Date range: {ts.FirstDate.Value:yyyy-MM-dd} to {ts.LastDate.Value:yyyy-MM-dd}");
            }

            // Top transaction categories
            if (context.TopTransactionCategories != null && context.TopTransactionCategories.Count > 0)
            {
                prompt.AppendLine("- Top expense categories (from transactions):");
                foreach (var (category, amount) in context.TopTransactionCategories)
                    prompt.AppendLine($"  * {category}: ${amount:N0}");
            }

            // Large transactions
            if (context.LargeTransactions != null && context.LargeTransactions.Count > 0)
            {
                prompt.AppendLine("- Largest recent transactions:");
                foreach (var tx in context.LargeTransactions)
                    prompt.AppendLine($"  * {tx.Date}: ${tx.Amount:N2} - {tx.Description} ({tx.Category})");
            }

            // All transactions for detailed month-specific queries
            if (context.AllTransactions != null && context.AllTransactions.Count > 0)
            {
                prompt.AppendLine("- All transactions:");
                foreach (var tx in context.AllTransactions)
                    prompt.AppendLine($"  * {tx.Date}: ${tx.Amount:N2} - {tx.Description} ({tx.Category})");
            }
        }
        else
        {
            prompt.AppendLine(
                "Note: The user has not yet entered financial data. Provide general financial advice and encourage them to set up their profile.");
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Represents the user's financial context gathered from the database.
    /// </summary>
    private class UserFinancialContext
    {
        public decimal AnnualIncome { get; set; }
        public decimal AnnualSpending { get; set; }
        public decimal CurrentNetWorth { get; set; }
        public decimal AnnualSavings { get; set; }
        public int CurrentAge { get; set; }
        public int RetirementAge { get; set; }
        public decimal MonthlyIncome { get; set; }
        public Dictionary<string, double>? BudgetAllocations { get; set; }
        public List<SavingsGoalInfo>? SavingsGoals { get; set; }

        // Budget report data from Google Drive spreadsheets
        public BudgetReportSummary? BudgetReportSummary { get; set; }
        public Dictionary<string, decimal>? TopSpendingCategories { get; set; }
        public List<string>? OverBudgetCategories { get; set; }
        public List<MonthlyBudgetDetail>? MonthlyBudgetDetails { get; set; }

        // Transaction data from Google Drive spreadsheets
        public TransactionSummary? TransactionSummary { get; set; }
        public Dictionary<string, decimal>? TopTransactionCategories { get; set; }
        public List<LargeTransactionInfo>? LargeTransactions { get; set; }
        public int IncomeTransactionCount { get; set; }
        public int ExpenseTransactionCount { get; set; }
        public List<LargeTransactionInfo>? AllTransactions { get; set; }

        public bool HasAnyData => AnnualIncome > 0 || AnnualSpending > 0 || CurrentNetWorth > 0 || MonthlyIncome > 0
                                  || BudgetReportSummary != null || TransactionSummary != null;
    }

    /// <summary>
    ///     Summary of the budget report from a Google Drive spreadsheet.
    /// </summary>
    private class BudgetReportSummary
    {
        public string FileName { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal TotalAnnualIncome { get; set; }
        public decimal TotalAnnualExpenses { get; set; }
        public decimal TotalAnnualSavings { get; set; }
        public double AverageSavingsRate { get; set; }
        public int MonthCount { get; set; }
        public int OverBudgetMonths { get; set; }
        public int UnderBudgetMonths { get; set; }
        public string? HighestExpenseMonth { get; set; }
        public decimal? HighestExpenseAmount { get; set; }
        public string? LowestExpenseMonth { get; set; }
        public decimal? LowestExpenseAmount { get; set; }
    }

    /// <summary>
    ///     Summary of transactions from a Google Drive spreadsheet.
    /// </summary>
    private class TransactionSummary
    {
        public string FileName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalIncome { get; set; }
        public DateTime? FirstDate { get; set; }
        public DateTime? LastDate { get; set; }
        public decimal AverageTransactionAmount { get; set; }
    }

    /// <summary>
    ///     Information about a large transaction.
    /// </summary>
    private class LargeTransactionInfo
    {
        public string Date { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    ///     Detailed budget data for a single month.
    /// </summary>
    private class MonthlyBudgetDetail
    {
        public string MonthName { get; set; } = string.Empty;
        public int MonthNumber { get; set; }
        public decimal BudgetedIncome { get; set; }
        public decimal ActualIncome { get; set; }
        public decimal BudgetedExpenses { get; set; }
        public decimal ActualExpenses { get; set; }
        public decimal NetSavings { get; set; }
        public double SavingsRate { get; set; }
        public decimal ExpenseVariance { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<BudgetCategoryDetail> Categories { get; set; } = new();
    }

    /// <summary>
    ///     Detailed budget data for a single category within a month.
    /// </summary>
    private class BudgetCategoryDetail
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal BudgetedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance { get; set; }
        public double PercentageUsed { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a savings goal's information.
    /// </summary>
    private class SavingsGoalInfo
    {
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public double ProgressPercent { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? TargetDate { get; set; }
    }
}