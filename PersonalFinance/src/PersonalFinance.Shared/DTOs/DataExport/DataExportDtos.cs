namespace PersonalFinance.Shared.DTOs.DataExport;

/// <summary>
/// A Data Export: every live record an Account holds in the app, one section per area. Soft-deleted records are
/// left out, and secrets (password hashes, security stamps, Google Drive API keys) are never included. Enum values
/// are written as their names so the downloaded file reads on its own.
/// </summary>
public class DataExportDto
{
    /// <summary>When the export was made (UTC).</summary>
    public DateTime ExportedAtUtc { get; set; }

    public DataExportAccountDto Account { get; set; } = new();

    /// <summary>The Account's policy consents, including the IP address recorded with each.</summary>
    public List<DataExportConsentDto> Consents { get; set; } = [];

    public List<DataExportNetWorthDto> NetWorthParameters { get; set; } = [];

    /// <summary>Budget Planner allocations.</summary>
    public List<DataExportBudgetAllocationDto> BudgetAllocations { get; set; } = [];

    /// <summary>Which Data Source Analysis and the Dashboard show.</summary>
    public string? BudgetDataSource { get; set; }

    /// <summary>The Account's chosen Language (<c>en</c>, <c>vi</c>), or null when it never chose one.</summary>
    public string? Language { get; set; }

    public List<DataExportBudgetCategoryDto> BudgetCategories { get; set; } = [];

    /// <summary>Budget Tracker Planned amounts and Actual Overrides, one row per category per month.</summary>
    public List<DataExportBudgetLineDto> BudgetMonthLines { get; set; } = [];

    public List<DataExportBudgetTransactionDto> BudgetTransactions { get; set; } = [];

    /// <summary>Google Drive connections, without API keys.</summary>
    public List<DataExportDriveConnectionDto> GoogleDriveConnections { get; set; } = [];

    /// <summary>Monthly summaries synced from Google Sheets.</summary>
    public List<DataExportSpreadsheetMonthDto> SpreadsheetMonths { get; set; } = [];

    /// <summary>Transactions synced from Google Sheets.</summary>
    public List<DataExportSpreadsheetTransactionDto> SpreadsheetTransactions { get; set; } = [];

    public List<DataExportSavingsGoalDto> SavingsGoals { get; set; } = [];

    public List<DataExportSavedArticleDto> SavedArticles { get; set; } = [];

    /// <summary>Money Priorities answers, path and Purpose Note; dollar figures are never stored, so none appear.</summary>
    public List<DataExportPriorityWalkDto> PriorityWalks { get; set; } = [];

    public DataExportCommunityDto Community { get; set; } = new();

    public List<DataExportFeedbackDto> Feedback { get; set; } = [];

    public List<DataExportNotificationDto> Notifications { get; set; } = [];

    /// <summary>AI Assistant usage: tokens and estimated cost per request, never the messages.</summary>
    public List<DataExportAiUsageDto> AiUsage { get; set; } = [];
}

/// <summary>The Account's sign-in identity.</summary>
public class DataExportAccountDto
{
    public string Id { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PhoneNumber { get; set; }

    public bool TwoFactorEnabled { get; set; }

    /// <summary>When the Account first accepted the policies at registration (UTC); null for older Accounts.</summary>
    public DateTime? RegisteredAtUtc { get; set; }

    public List<string> Roles { get; set; } = [];

    /// <summary>External sign-in providers linked to the Account, e.g. Google.</summary>
    public List<string> ExternalLogins { get; set; } = [];
}

public class DataExportConsentDto
{
    public string PolicyVersion { get; set; } = string.Empty;

    public bool AcceptedTerms { get; set; }

    public bool AcceptedPrivacy { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class DataExportNetWorthDto
{
    public decimal StartingBalance { get; set; }

    public decimal AnnualIncome { get; set; }

    public decimal AnnualSpending { get; set; }

    public decimal AnnualSavings { get; set; }

    public decimal AggressiveSavings { get; set; }

    public int CurrentAge { get; set; }

    public int RetirementAge { get; set; }

    public decimal BaseRoi { get; set; }

    public decimal ConservativeRoi { get; set; }

    public decimal AggressiveRoi { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public class DataExportBudgetAllocationDto
{
    public string Period { get; set; } = string.Empty;

    public decimal MonthlyIncome { get; set; }

    public decimal HousingAmount { get; set; }

    public decimal FoodAmount { get; set; }

    public decimal TransportationAmount { get; set; }

    public decimal UtilitiesAmount { get; set; }

    public decimal InsuranceAmount { get; set; }

    public decimal HealthcareAmount { get; set; }

    public decimal SavingsAmount { get; set; }

    public decimal DebtRepaymentAmount { get; set; }

    public decimal EntertainmentAmount { get; set; }

    public decimal MiscellaneousAmount { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public class DataExportBudgetCategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Expense or Income.</summary>
    public string Kind { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public class DataExportBudgetLineDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>The Budget Category's name, kept even when the category was deleted later.</summary>
    public string Category { get; set; } = string.Empty;

    public decimal PlannedAmount { get; set; }

    public decimal? ActualOverride { get; set; }
}

public class DataExportBudgetTransactionDto
{
    public DateOnly Date { get; set; }

    /// <summary>The Budget Category's name, kept even when the category was deleted later.</summary>
    public string Category { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;
}

public class DataExportDriveConnectionDto
{
    public string Name { get; set; } = string.Empty;

    public string FolderUrl { get; set; } = string.Empty;

    public string SyncStatus { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastSyncedAtUtc { get; set; }
}

public class DataExportSpreadsheetMonthDto
{
    public string FileName { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal BudgetedIncome { get; set; }

    public decimal ActualIncome { get; set; }

    public decimal BudgetedExpenses { get; set; }

    public decimal ActualExpenses { get; set; }

    public decimal? StartingBalance { get; set; }

    public decimal? EndingBalance { get; set; }

    public DateTime SyncedAtUtc { get; set; }
}

public class DataExportSpreadsheetTransactionDto
{
    public DateTime Date { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
}

public class DataExportSavingsGoalDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal TargetAmount { get; set; }

    public decimal CurrentAmount { get; set; }

    public DateTime? TargetDate { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The goal's deposits and withdrawals, oldest first.</summary>
    public List<DataExportContributionDto> Contributions { get; set; } = [];
}

public class DataExportContributionDto
{
    public DateOnly Date { get; set; }

    /// <summary>Positive for a deposit, negative for a withdrawal.</summary>
    public decimal Amount { get; set; }

    /// <summary>Regular, Opening (the starting amount) or Adjustment (a balance correction).</summary>
    public string Kind { get; set; } = string.Empty;

    public string? Note { get; set; }
}

public class DataExportSavedArticleDto
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime SavedAtUtc { get; set; }
}

/// <summary>A Priority Walk. A null answer means the question was not asked on the Walk's path.</summary>
public class DataExportPriorityWalkDto
{
    public decimal MonthlyAmount { get; set; }

    public string? Purpose { get; set; }

    public decimal DebtApr { get; set; }

    public decimal? DebtBalance { get; set; }

    public decimal? EmergencyFundMonths { get; set; }

    public decimal? MonthlyExpenses { get; set; }

    public bool? HasFullEmployerMatch { get; set; }

    public decimal? EmployerMatchPercent { get; set; }

    public int? HorizonYears { get; set; }

    public string? AgeBand { get; set; }

    public bool? AtContributionLimit { get; set; }

    public int TreeVersion { get; set; }

    public int? LimitsTaxYear { get; set; }

    /// <summary>The ordered node ids of the path through the Priority Tree, e.g. <c>S,Q1,Q2,Q3,Q4,L5</c>.</summary>
    public string Path { get; set; } = string.Empty;

    public string LeafId { get; set; } = string.Empty;

    public string? PurposeNote { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>What the Account wrote and reacted to in Community.</summary>
public class DataExportCommunityDto
{
    public List<DataExportStoryDto> Stories { get; set; } = [];

    public List<DataExportCommentDto> Comments { get; set; } = [];

    public List<DataExportStoryLikeDto> StoryLikes { get; set; } = [];

    public List<DataExportCommentLikeDto> CommentLikes { get; set; } = [];

    public List<DataExportPollVoteDto> PollVotes { get; set; } = [];
}

public class DataExportStoryDto
{
    public string AuthorName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public DateTime PublishedAtUtc { get; set; }
}

public class DataExportCommentDto
{
    public string AuthorName { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    /// <summary>The poll the comment was posted on, if any.</summary>
    public int? PollId { get; set; }

    /// <summary>The case study the comment was posted on, if any.</summary>
    public string? CaseStudyTitle { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class DataExportStoryLikeDto
{
    public int StoryId { get; set; }

    /// <summary>The liked story's title; null when the story is no longer available.</summary>
    public string? StoryTitle { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class DataExportCommentLikeDto
{
    public int CommentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class DataExportPollVoteDto
{
    public string? Question { get; set; }

    public string? Answer { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class DataExportFeedbackDto
{
    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? PageUrl { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public class DataExportNotificationDto
{
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class DataExportAiUsageDto
{
    public string Endpoint { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
