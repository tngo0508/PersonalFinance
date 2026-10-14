using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AiAssistantService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _modelName;

    public AiAssistantService(
        AppDbContext dbContext,
        ILogger<AiAssistantService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
        _modelName = configuration.GetValue<string>("OpenAI:Model") ?? "gpt-4o-mini";
    }

    public async Task<AiChatResponseDto> SendChatMessageAsync(string userId, AiChatRequestDto request, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.GetValue<string>("OpenAI:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured. Returning a fallback response.");
            return new AiChatResponseDto
            {
                Response = "AI assistant is currently unavailable. Please configure the OpenAI API key to enable this feature.",
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

    /// <summary>
    /// Gets the user's financial context from the database.
    /// </summary>
    private async Task<UserFinancialContext> GetUserFinancialContextAsync(string userId, CancellationToken cancellationToken)
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving financial context for user {UserId}", userId);
        }

        return context;
    }

    /// <summary>
    /// Builds the system prompt for the AI assistant.
    /// </summary>
    private string BuildSystemPrompt(UserFinancialContext context)
    {
        var prompt = new System.Text.StringBuilder();
        prompt.AppendLine("You are a helpful and friendly personal finance assistant for the PersonalFinance app.");
        prompt.AppendLine("You have access to the user's financial data and can provide insights on:");
        prompt.AppendLine("- Net worth projections and retirement planning");
        prompt.AppendLine("- Budget allocation recommendations");
        prompt.AppendLine("- Savings goal progress");
        prompt.AppendLine("- General financial literacy and advice");
        prompt.AppendLine("- Transaction analysis and spending patterns");
        prompt.AppendLine();
        prompt.AppendLine("IMPORTANT RULES:");
        prompt.AppendLine("1. Always be encouraging and positive, but honest about financial situations.");
        prompt.AppendLine("2. Provide actionable, specific advice based on the user's data.");
        prompt.AppendLine("3. Clarify that you're an AI assistant and recommendations should be verified with a qualified financial advisor.");
        prompt.AppendLine("4. Keep responses concise and easy to understand - avoid overly technical jargon.");
        prompt.AppendLine("5. Use bullet points and formatting to make responses readable.");
        prompt.AppendLine("6. Never make up financial data - only use the data provided in the context below.");
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
        }
        else
        {
            prompt.AppendLine("Note: The user has not yet entered financial data. Provide general financial advice and encourage them to set up their profile.");
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

        public bool HasAnyData => AnnualIncome > 0 || AnnualSpending > 0 || CurrentNetWorth > 0 || MonthlyIncome > 0;
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
