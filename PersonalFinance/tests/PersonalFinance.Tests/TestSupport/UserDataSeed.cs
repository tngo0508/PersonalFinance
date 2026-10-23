using Microsoft.AspNetCore.Identity;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.Tests;

/// <summary>
/// Seeds an Account's data for tests that must cover every user-owned table (Data Export, Deletion Request).
/// Add a row here when you add a user-owned entity; <c>AccountDeletionServiceTests</c> fails until you do.
/// </summary>
public static class UserDataSeed
{
    /// <summary>
    /// One row in every user-owned table (plus a poll and the Identity user) for <paramref name="userId"/>, each with <paramref name="marker"/> in a
    /// text field, so it is clear whose records a result holds.
    /// </summary>
    public static void OneOfEverything(AppDbContext db, string userId, string marker)
    {
        db.Users.Add(new IdentityUser { Id = userId, UserName = $"{marker}@example.com", Email = $"{marker}@example.com" });
        db.UserConsents.Add(new UserConsent { UserId = userId, PolicyVersion = marker });
        db.NetWorthParameters.Add(new NetWorthParameters { UserId = userId, StartingBalance = 1000m });
        db.UserBudgetAllocations.Add(new UserBudgetAllocation { UserId = userId, Period = marker });
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference { UserId = userId, Source = BudgetDataSource.BudgetTracker });

        var category = new BudgetCategory { UserId = userId, Name = $"{marker} category", Kind = BudgetCategoryKind.Expense };
        var plan = new MonthlyBudgetPlan { UserId = userId, Year = 2026, Month = 3 };
        db.MonthlyBudgetLines.Add(new MonthlyBudgetLine { UserId = userId, Plan = plan, Category = category, PlannedAmount = 50m });
        db.BudgetTransactions.Add(new BudgetTransaction
        { UserId = userId, Category = category, Date = new DateOnly(2026, 3, 2), Description = $"{marker} coffee" });

        var connection = new GoogleDriveConnection { UserId = userId, Name = $"{marker} folder", FolderId = "f", FolderUrl = "u" };
        var snapshot = new SpreadsheetBudgetSnapshot
        { UserId = userId, Connection = connection, FileName = $"{marker}.xlsx", FileId = "file", CategoryBreakdownJson = "{}" };
        db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
        { UserId = userId, Snapshot = snapshot, Connection = connection, Description = $"{marker} rent" });

        db.SavingsGoals.Add(new SavingsGoal { UserId = userId, Name = $"{marker} goal" });
        db.SavedArticles.Add(new SavedArticle { UserId = userId, Title = $"{marker} article", Url = "https://example.com" });

        var story = new CommunityStory { UserId = userId, AuthorName = marker, Title = $"{marker} story" };
        var comment = new CommunityComment
        { UserId = userId, AuthorName = marker, Text = $"{marker} comment", CaseStudyTitle = $"{marker} case study" };
        db.CommunityStoryLikes.Add(new CommunityStoryLike { UserId = userId, Story = story });
        db.CommunityCommentLikes.Add(new CommunityCommentLike { UserId = userId, Comment = comment });
        var poll = new CommunityPoll { PollCode = $"{marker}-poll", Question = $"{marker} question?" };
        var option = new CommunityPollOption { Poll = poll, Text = "Yes" };
        db.CommunityPollVotes.Add(new CommunityPollVote { UserId = userId, Poll = poll, Option = option });

        db.Feedback.Add(new FeedbackEntity { UserId = userId, Category = FeedbackCategory.Idea, Message = $"{marker} idea" });
        db.Notifications.Add(new Notification { UserId = userId, Title = $"{marker} notice", Message = "m" });
        db.AiUsageRecords.Add(new AiUsageRecord { UserId = userId, Endpoint = $"{marker}-chat" });
    }
}
