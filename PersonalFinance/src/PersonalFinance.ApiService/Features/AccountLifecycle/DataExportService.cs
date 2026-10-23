using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.DataExport;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.AccountLifecycle;

/// <summary>
/// EF Core implementation of <see cref="IDataExportService"/>. One read-only query per section, each filtered by
/// the Account and ordered deterministically; the global query filters leave out soft-deleted rows.
/// </summary>
internal sealed class DataExportService : IDataExportService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public DataExportService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<DataExportDto> ExportAsync(string userId, CancellationToken cancellationToken = default)
    {
        List<DataExportConsentDto> consents = await _db.UserConsents.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => new DataExportConsentDto
            {
                PolicyVersion = c.PolicyVersion,
                AcceptedTerms = c.AcceptedTerms,
                AcceptedPrivacy = c.AcceptedPrivacy,
                IpAddress = c.IpAddress,
                CreatedAtUtc = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var export = new DataExportDto
        {
            ExportedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            Account = await AccountAsync(userId, consents, cancellationToken),
            Consents = consents
        };

        export.NetWorthParameters = await _db.NetWorthParameters.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Id)
            .Select(p => new DataExportNetWorthDto
            {
                StartingBalance = p.StartingBalance,
                AnnualIncome = p.AnnualIncome,
                AnnualSpending = p.AnnualSpending,
                AnnualSavings = p.AnnualSavings,
                AggressiveSavings = p.AggressiveSavings,
                CurrentAge = p.CurrentAge,
                RetirementAge = p.RetirementAge,
                BaseRoi = p.BaseRoi,
                ConservativeRoi = p.ConservativeRoi,
                AggressiveRoi = p.AggressiveRoi,
                UpdatedAtUtc = p.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        export.BudgetAllocations = await _db.UserBudgetAllocations.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Period).ThenBy(a => a.Id)
            .Select(a => new DataExportBudgetAllocationDto
            {
                Period = a.Period,
                MonthlyIncome = a.MonthlyIncome,
                HousingAmount = a.HousingAmount,
                FoodAmount = a.FoodAmount,
                TransportationAmount = a.TransportationAmount,
                UtilitiesAmount = a.UtilitiesAmount,
                InsuranceAmount = a.InsuranceAmount,
                HealthcareAmount = a.HealthcareAmount,
                SavingsAmount = a.SavingsAmount,
                DebtRepaymentAmount = a.DebtRepaymentAmount,
                EntertainmentAmount = a.EntertainmentAmount,
                MiscellaneousAmount = a.MiscellaneousAmount,
                UpdatedAtUtc = a.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        BudgetDataSource? source = await _db.UserBudgetSourcePreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .Select(p => (Shared.Enums.BudgetDataSource?)p.Source)
            .FirstOrDefaultAsync(cancellationToken);
        export.BudgetDataSource = source?.ToString();

        export.BudgetCategories = (await _db.BudgetCategories.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .Select(c => new { c.Id, c.Name, c.Kind, c.SortOrder })
                .ToListAsync(cancellationToken))
            .Select(c => new DataExportBudgetCategoryDto
            { Id = c.Id, Name = c.Name, Kind = c.Kind.ToString(), SortOrder = c.SortOrder })
            .ToList();

        export.BudgetMonthLines = await BudgetMonthLinesQuery(userId).ToListAsync(cancellationToken);
        export.BudgetTransactions = await BudgetTransactionsQuery(userId).ToListAsync(cancellationToken);

        export.GoogleDriveConnections = await _db.GoogleDriveConnections.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .Select(c => new DataExportDriveConnectionDto
            {
                Name = c.Name,
                FolderUrl = c.FolderUrl,
                SyncStatus = c.SyncStatus,
                CreatedAtUtc = c.CreatedAtUtc,
                LastSyncedAtUtc = c.LastSyncedAtUtc
            })
            .ToListAsync(cancellationToken);

        export.SpreadsheetMonths = await _db.SpreadsheetBudgetSnapshots.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Year).ThenBy(s => s.MonthNumber).ThenBy(s => s.Id)
            .Select(s => new DataExportSpreadsheetMonthDto
            {
                FileName = s.FileName,
                Year = s.Year,
                Month = s.MonthNumber,
                BudgetedIncome = s.BudgetedIncome,
                ActualIncome = s.ActualIncome,
                BudgetedExpenses = s.BudgetedExpenses,
                ActualExpenses = s.ActualExpenses,
                StartingBalance = s.StartingBalance,
                EndingBalance = s.EndingBalance,
                SyncedAtUtc = s.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);

        export.SpreadsheetTransactions = await _db.SpreadsheetTransactionRecords.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Date).ThenBy(t => t.Id)
            .Select(t => new DataExportSpreadsheetTransactionDto
            {
                Date = t.Date,
                Amount = t.Amount,
                Description = t.Description,
                Category = t.Category,
                Type = t.Type
            })
            .ToListAsync(cancellationToken);

        export.SavingsGoals = await _db.SavingsGoals.AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.CreatedAt).ThenBy(g => g.Id)
            .Select(g => new DataExportSavingsGoalDto
            {
                Name = g.Name,
                Description = g.Description,
                TargetAmount = g.TargetAmount,
                CurrentAmount = g.CurrentAmount,
                TargetDate = g.TargetDate,
                IsCompleted = g.IsCompleted,
                CreatedAtUtc = g.CreatedAt
            })
            .ToListAsync(cancellationToken);

        export.SavedArticles = await _db.SavedArticles.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.SavedAtUtc).ThenBy(a => a.Id)
            .Select(a => new DataExportSavedArticleDto
            {
                Title = a.Title,
                Url = a.Url,
                Source = a.Source,
                Category = a.Category,
                Notes = a.Notes,
                SavedAtUtc = a.SavedAtUtc
            })
            .ToListAsync(cancellationToken);

        export.Community = await CommunityAsync(userId, cancellationToken);

        export.Feedback = (await _db.Feedback.AsNoTracking()
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.CreatedAt).ThenBy(f => f.Id)
                .Select(f => new { f.Category, f.Message, f.PageUrl, f.Status, f.CreatedAt })
                .ToListAsync(cancellationToken))
            .Select(f => new DataExportFeedbackDto
            {
                Category = f.Category.ToString(),
                Message = f.Message,
                PageUrl = f.PageUrl,
                Status = f.Status.ToString(),
                CreatedAtUtc = f.CreatedAt
            })
            .ToList();

        export.Notifications = (await _db.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
                .Select(n => new { n.Type, n.Title, n.Message, n.IsRead, n.CreatedAt })
                .ToListAsync(cancellationToken))
            .Select(n => new DataExportNotificationDto
            {
                Type = n.Type.ToString(),
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAtUtc = n.CreatedAt
            })
            .ToList();

        export.AiUsage = await _db.AiUsageRecords.AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Select(r => new DataExportAiUsageDto
            {
                Endpoint = r.Endpoint,
                PromptTokens = r.PromptTokens,
                CompletionTokens = r.CompletionTokens,
                EstimatedCostUsd = r.EstimatedCostUsd,
                CreatedAtUtc = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return export;
    }

    /// <summary>
    /// Budget Tracker transactions with their category's name, deleted categories included, because a deleted
    /// Budget Category stays visible in past months that used it. <c>IgnoreQueryFilters</c> switches off every
    /// filter in the query, so soft-deleted transactions are excluded explicitly.
    /// Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<DataExportBudgetTransactionDto> BudgetTransactionsQuery(string userId) =>
        from transaction in _db.BudgetTransactions.IgnoreQueryFilters().AsNoTracking()
        where transaction.UserId == userId && !transaction.IsDeleted
        join category in _db.BudgetCategories.AsNoTracking()
            on transaction.CategoryId equals category.Id into categories
        from category in categories.DefaultIfEmpty()
        orderby transaction.Date, transaction.Id
        select new DataExportBudgetTransactionDto
        {
            Date = transaction.Date,
            Category = category == null ? string.Empty : category.Name,
            Amount = transaction.Amount,
            Description = transaction.Description
        };

    /// <summary>
    /// Budget Tracker Planned amounts and Actual Overrides of live months, with the category's name (deleted
    /// categories included, as for transactions). Filters are off for the whole query, so soft-deleted lines and
    /// months are excluded explicitly. Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<DataExportBudgetLineDto> BudgetMonthLinesQuery(string userId) =>
        from line in _db.MonthlyBudgetLines.IgnoreQueryFilters().AsNoTracking()
        where line.UserId == userId && !line.IsDeleted
        join plan in _db.MonthlyBudgetPlans.AsNoTracking() on line.PlanId equals plan.Id
        where !plan.IsDeleted
        join category in _db.BudgetCategories.AsNoTracking()
            on line.CategoryId equals category.Id into categories
        from category in categories.DefaultIfEmpty()
        orderby plan.Year, plan.Month, line.Id
        select new DataExportBudgetLineDto
        {
            Year = plan.Year,
            Month = plan.Month,
            Category = category == null ? string.Empty : category.Name,
            PlannedAmount = line.PlannedAmount,
            ActualOverride = line.ActualOverride
        };

    /// <summary>
    /// Stories the Account liked, with the story's title while the story is still live.
    /// Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<DataExportStoryLikeDto> StoryLikesQuery(string userId) =>
        from like in _db.CommunityStoryLikes.AsNoTracking()
        where like.UserId == userId
        join story in _db.CommunityStories.AsNoTracking() on like.StoryId equals story.Id into stories
        from story in stories.DefaultIfEmpty()
        orderby like.CreatedAtUtc, like.Id
        select new DataExportStoryLikeDto
        {
            StoryId = like.StoryId,
            StoryTitle = story == null ? null : story.Title,
            CreatedAtUtc = like.CreatedAtUtc
        };

    /// <summary>
    /// The Account's poll votes with the poll's question and the chosen option's text.
    /// Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<DataExportPollVoteDto> PollVotesQuery(string userId) =>
        from vote in _db.CommunityPollVotes.AsNoTracking()
        where vote.UserId == userId
        join poll in _db.CommunityPolls.AsNoTracking() on vote.PollId equals (int?)poll.Id into polls
        from poll in polls.DefaultIfEmpty()
        join option in _db.CommunityPollOptions.AsNoTracking() on vote.OptionId equals (int?)option.Id into options
        from option in options.DefaultIfEmpty()
        orderby vote.CreatedAtUtc, vote.Id
        select new DataExportPollVoteDto
        {
            Question = poll == null ? null : poll.Question,
            Answer = option == null ? null : option.Text,
            CreatedAtUtc = vote.CreatedAtUtc
        };

    /// <summary>
    /// The names of the Account's Roles, in name order. Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<string> AccountRolesQuery(string userId) =>
        from userRole in _db.UserRoles.AsNoTracking()
        where userRole.UserId == userId
        join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
        orderby role.Name
        select role.Name ?? string.Empty;

    private async Task<DataExportAccountDto> AccountAsync(string userId, List<DataExportConsentDto> consents,
        CancellationToken cancellationToken)
    {
        DataExportAccountDto account = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new DataExportAccountDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                EmailConfirmed = u.EmailConfirmed,
                PhoneNumber = u.PhoneNumber,
                TwoFactorEnabled = u.TwoFactorEnabled
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new DataExportAccountDto { Id = userId };

        // Consent is recorded at registration, so the first consent dates the Account.
        account.RegisteredAtUtc = consents.Count == 0 ? null : consents[0].CreatedAtUtc;
        account.Roles = await AccountRolesQuery(userId).ToListAsync(cancellationToken);
        account.ExternalLogins = await _db.UserLogins.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.LoginProvider).ThenBy(l => l.ProviderKey)
            .Select(l => l.ProviderDisplayName ?? l.LoginProvider)
            .ToListAsync(cancellationToken);
        return account;
    }

    private async Task<DataExportCommunityDto> CommunityAsync(string userId, CancellationToken cancellationToken)
    {
        return new DataExportCommunityDto
        {
            Stories = await _db.CommunityStories.AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.PublishedAtUtc).ThenBy(s => s.Id)
                .Select(s => new DataExportStoryDto
                {
                    AuthorName = s.AuthorName,
                    Title = s.Title,
                    Summary = s.Summary,
                    Body = s.Body,
                    Category = s.Category,
                    PublishedAtUtc = s.PublishedAtUtc
                })
                .ToListAsync(cancellationToken),
            Comments = await _db.CommunityComments.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
                .Select(c => new DataExportCommentDto
                {
                    AuthorName = c.AuthorName,
                    Text = c.Text,
                    Topic = c.Topic,
                    PollId = c.PollId,
                    CaseStudyTitle = c.CaseStudyTitle,
                    CreatedAtUtc = c.CreatedAtUtc
                })
                .ToListAsync(cancellationToken),
            StoryLikes = await StoryLikesQuery(userId).ToListAsync(cancellationToken),
            CommentLikes = await _db.CommunityCommentLikes.AsNoTracking()
                .Where(l => l.UserId == userId)
                .OrderBy(l => l.CreatedAtUtc).ThenBy(l => l.Id)
                .Select(l => new DataExportCommentLikeDto { CommentId = l.CommentId, CreatedAtUtc = l.CreatedAtUtc })
                .ToListAsync(cancellationToken),
            PollVotes = await PollVotesQuery(userId).ToListAsync(cancellationToken)
        };
    }
}
