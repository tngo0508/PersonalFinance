using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Data;

/// <summary>
/// Database context inheriting from IdentityDbContext for ASP.NET Core Identity authentication tables
/// and implementing IDataProtectionKeyContext for persistent Data Protection keyring across container restarts.
/// </summary>
public class AppDbContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext
{
    /// <summary>
    /// Non-generic options constructor so provider-specific subclasses can forward their typed options.
    /// </summary>
    public AppDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// User Google Drive connections configuration table set.
    /// </summary>
    public DbSet<GoogleDriveConnection> GoogleDriveConnections => Set<GoogleDriveConnection>();

    /// <summary>
    /// Locally cached Google Drive files and metadata table set.
    /// </summary>
    public DbSet<GoogleDriveCachedFile> GoogleDriveCachedFiles => Set<GoogleDriveCachedFile>();

    /// <summary>
    /// User-saved net worth projection parameters table set (one record per user).
    /// </summary>
    public DbSet<NetWorthParameters> NetWorthParameters => Set<NetWorthParameters>();

    /// <summary>
    /// Community poll questions table set.
    /// </summary>
    public DbSet<CommunityPoll> CommunityPolls => Set<CommunityPoll>();

    /// <summary>
    /// Community poll options table set.
    /// </summary>
    public DbSet<CommunityPollOption> CommunityPollOptions => Set<CommunityPollOption>();

    /// <summary>
    /// Community poll votes (user -> option) table set.
    /// </summary>
    public DbSet<CommunityPollVote> CommunityPollVotes => Set<CommunityPollVote>();

    /// <summary>
    /// Community comments on polls or case studies table set.
    /// </summary>
    public DbSet<CommunityComment> CommunityComments => Set<CommunityComment>();

    /// <summary>
    /// Community comment likes (user -> comment) table set.
    /// </summary>
    public DbSet<CommunityCommentLike> CommunityCommentLikes => Set<CommunityCommentLike>();

    /// <summary>
    /// Community user-submitted stories table set.
    /// </summary>
    public DbSet<CommunityStory> CommunityStories => Set<CommunityStory>();

    /// <summary>
    /// Community story likes (user -> story) table set.
    /// </summary>
    public DbSet<CommunityStoryLike> CommunityStoryLikes => Set<CommunityStoryLike>();

    /// <summary>
    /// User budget allocations table set.
    /// </summary>
    public DbSet<UserBudgetAllocation> UserBudgetAllocations => Set<UserBudgetAllocation>();

    /// <summary>
    /// User savings goals table set.
    /// </summary>
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();

    /// <summary>
    /// User saved articles (watchlist) table set.
    /// </summary>
    public DbSet<SavedArticle> SavedArticles => Set<SavedArticle>();

    /// <summary>
    ///     Spreadsheet budget snapshots table set — stores parsed monthly budget report data for analysis.
    /// </summary>
    public DbSet<SpreadsheetBudgetSnapshot> SpreadsheetBudgetSnapshots => Set<SpreadsheetBudgetSnapshot>();

    /// <summary>
    ///     Spreadsheet transaction records table set — stores individual transactions for analysis.
    /// </summary>
    public DbSet<SpreadsheetTransactionRecord> SpreadsheetTransactionRecords => Set<SpreadsheetTransactionRecord>();

    /// <summary>
    ///     Budget Tracker categories table set — user-owned expense and income categories.
    /// </summary>
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();

    /// <summary>
    ///     Budget Tracker monthly plans table set — one plan per user and calendar month.
    /// </summary>
    public DbSet<MonthlyBudgetPlan> MonthlyBudgetPlans => Set<MonthlyBudgetPlan>();

    /// <summary>
    ///     Budget Tracker monthly plan lines table set — planned amounts and optional Actual overrides per category and month.
    /// </summary>
    public DbSet<MonthlyBudgetLine> MonthlyBudgetLines => Set<MonthlyBudgetLine>();

    /// <summary>
    ///     Budget Tracker transactions table set — dated expense and income entries.
    /// </summary>
    public DbSet<BudgetTransaction> BudgetTransactions => Set<BudgetTransaction>();

    /// <summary>
    ///     Budget data source preferences table set — the source a user explicitly selected for Analysis and the Dashboard.
    /// </summary>
    public DbSet<UserBudgetSourcePreference> UserBudgetSourcePreferences => Set<UserBudgetSourcePreference>();

    /// <summary>
    /// User notifications table set — stores budget alerts, savings milestones, and sync status notifications.
    /// </summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>
    /// ASP.NET Core Data Protection keys table set for preserving cookie/token encryption across restarts.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filters for soft deletes — automatically exclude soft-deleted records
        modelBuilder.Entity<NetWorthParameters>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SavingsGoal>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SavedArticle>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserBudgetAllocation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GoogleDriveConnection>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SpreadsheetBudgetSnapshot>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SpreadsheetTransactionRecord>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CommunityStory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CommunityComment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CommunityCommentLike>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CommunityPollVote>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CommunityStoryLike>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BudgetCategory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<MonthlyBudgetPlan>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<MonthlyBudgetLine>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BudgetTransaction>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserBudgetSourcePreference>().HasQueryFilter(e => !e.IsDeleted);

        // Explicit Identity key/FK lengths so both providers (and design-time scaffolding) produce
        // bounded string columns. SQL Server cannot index or key nvarchar(max).
        const int identityKeyMaxLength = 450;
        const int identityLoginTokenMaxLength = 128;

        modelBuilder.Entity<IdentityUser>(entity => { entity.Property(e => e.Id).HasMaxLength(identityKeyMaxLength); });

        modelBuilder.Entity<IdentityRole>(entity => { entity.Property(e => e.Id).HasMaxLength(identityKeyMaxLength); });

        modelBuilder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.Property(e => e.LoginProvider).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.ProviderKey).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
            entity.Property(e => e.RoleId).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
            entity.Property(e => e.LoginProvider).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.Name).HasMaxLength(identityLoginTokenMaxLength);
        });

        modelBuilder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.Property(e => e.RoleId).HasMaxLength(identityKeyMaxLength);
        });


        // Configure GoogleDriveConnection entity
        modelBuilder.Entity<GoogleDriveConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FolderId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FolderUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.EncryptedApiKey).HasMaxLength(2000);
            entity.Property(e => e.MaskedApiKey).HasMaxLength(100);
            entity.Property(e => e.SyncStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.FolderId });

            entity.HasMany(e => e.CachedFiles)
                .WithOne(e => e.Connection)
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        // Configure GoogleDriveCachedFile entity
        modelBuilder.Entity<GoogleDriveCachedFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DriveFileId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MimeType).HasMaxLength(250);
            entity.Property(e => e.SizeFormatted).HasMaxLength(50);
            entity.Property(e => e.FileType).HasMaxLength(100);
            entity.Property(e => e.IconBadgeClass).HasMaxLength(50);
            entity.Property(e => e.WebViewLink).HasMaxLength(1000);
            entity.Property(e => e.IconLink).HasMaxLength(1000);
            entity.Property(e => e.ThumbnailLink).HasMaxLength(1000);

            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => new { e.ConnectionId, e.DriveFileId });
        });

        // Configure NetWorthParameters entity — one record per user
        modelBuilder.Entity<NetWorthParameters>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => e.UserId).IsUnique();

            // Explicit precision for decimal properties to avoid silent truncation warnings
            const int decimalPrecision = 18;
            const int decimalScale = 4;
            entity.Property(e => e.StartingBalance).HasPrecision(decimalPrecision, decimalScale);
            entity.Property(e => e.AnnualSavings).HasPrecision(decimalPrecision, decimalScale);
            entity.Property(e => e.AnnualIncome).HasPrecision(decimalPrecision, decimalScale);
            entity.Property(e => e.AnnualSpending).HasPrecision(decimalPrecision, decimalScale);
            entity.Property(e => e.BaseRoi).HasPrecision(decimalPrecision, decimalScale);
            entity.Property(e => e.ConservativeRoi).HasPrecision(decimalPrecision, decimalScale);
            entity.Property(e => e.AggressiveRoi).HasPrecision(decimalPrecision, decimalScale);
            entity.Property(e => e.AggressiveSavings).HasPrecision(decimalPrecision, decimalScale);
        });

        // --- Community entities ---

        // Configure CommunityPoll entity
        modelBuilder.Entity<CommunityPoll>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PollCode).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Question).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.PollCode).IsUnique();
            entity.HasIndex(e => e.CreatedAtUtc);

            entity.HasMany(e => e.Options)
                .WithOne(e => e.Poll)
                .HasForeignKey(e => e.PollId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Votes)
                .WithOne(e => e.Poll)
                .HasForeignKey(e => e.PollId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CommunityPollOption entity
        modelBuilder.Entity<CommunityPollOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(200);

            entity.HasIndex(e => e.PollId);

            entity.HasMany(e => e.Votes)
                .WithOne(e => e.Option)
                .HasForeignKey(e => e.OptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CommunityPollVote entity — one vote per user per poll
        modelBuilder.Entity<CommunityPollVote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => new { e.UserId, e.PollId }).IsUnique();
            entity.HasIndex(e => e.OptionId);
        });

        // Configure CommunityComment entity
        modelBuilder.Entity<CommunityComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.AuthorName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Topic).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CaseStudyTitle).HasMaxLength(500);
            entity.HasIndex(e => e.PollId);
            entity.HasIndex(e => e.CreatedAtUtc);

            entity.HasOne(e => e.Poll)
                .WithMany()
                .HasForeignKey(e => e.PollId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.LikesList)
                .WithOne(e => e.Comment)
                .HasForeignKey(e => e.CommentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CommunityCommentLike entity — one like per user per comment
        modelBuilder.Entity<CommunityCommentLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => new { e.UserId, e.CommentId }).IsUnique();
            entity.HasIndex(e => e.CommentId);
        });

        // Configure CommunityStory entity
        modelBuilder.Entity<CommunityStory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.AuthorName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Summary).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(10000);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.PublishedAtUtc);
            entity.HasIndex(e => e.IsFeatured);

            entity.HasMany(e => e.LikesList)
                .WithOne(e => e.Story)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CommunityStoryLike entity — one like per user per story
        modelBuilder.Entity<CommunityStoryLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => new { e.UserId, e.StoryId }).IsUnique();
            entity.HasIndex(e => e.StoryId);
        });

        // --- Personal Finance entities ---

        // Configure UserBudgetAllocation entity
        modelBuilder.Entity<UserBudgetAllocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Period).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => new { e.UserId, e.Period }).IsUnique();
            const int decPrec = 18;
            const int decScale = 2;
            entity.Property(e => e.MonthlyIncome).HasPrecision(decPrec, decScale);
            entity.Property(e => e.HousingPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.FoodPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.TransportationPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.UtilitiesPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.InsurancePercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.HealthcarePercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.SavingsPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.DebtRepaymentPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.EntertainmentPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.MiscellaneousPercent).HasPrecision(decPrec, decScale);
            entity.Property(e => e.HousingAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.FoodAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.TransportationAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.UtilitiesAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.InsuranceAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.HealthcareAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.SavingsAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.DebtRepaymentAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.EntertainmentAmount).HasPrecision(decPrec, decScale);
            entity.Property(e => e.MiscellaneousAmount).HasPrecision(decPrec, decScale);
        });

        // Configure SavingsGoal entity
        modelBuilder.Entity<SavingsGoal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.UserId);
            const int decPrec2 = 18;
            const int decScale2 = 2;
            entity.Property(e => e.TargetAmount).HasPrecision(decPrec2, decScale2);
            entity.Property(e => e.CurrentAmount).HasPrecision(decPrec2, decScale2);
        });

        // Configure SavedArticle entity
        modelBuilder.Entity<SavedArticle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.ImageUrl).HasMaxLength(2000);
            entity.HasIndex(e => new { e.UserId, e.Url }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // --- Spreadsheet Analysis entities ---

        // Configure SpreadsheetBudgetSnapshot entity
        modelBuilder.Entity<SpreadsheetBudgetSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MonthName).IsRequired().HasMaxLength(20);
            const int decPrec = 18;
            const int decScale = 4;
            entity.Property(e => e.BudgetedIncome).HasPrecision(decPrec, decScale);
            entity.Property(e => e.ActualIncome).HasPrecision(decPrec, decScale);
            entity.Property(e => e.BudgetedExpenses).HasPrecision(decPrec, decScale);
            entity.Property(e => e.ActualExpenses).HasPrecision(decPrec, decScale);
            entity.Property(e => e.StartingBalance).HasPrecision(decPrec, decScale);
            entity.Property(e => e.EndingBalance).HasPrecision(decPrec, decScale);
            entity.Property(e => e.CategoryBreakdownJson).HasMaxLength(10000);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => new { e.UserId, e.Year, e.MonthNumber });

            entity.HasOne(e => e.Connection)
                .WithMany()
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Transactions)
                .WithOne(e => e.Snapshot)
                .HasForeignKey(e => e.SnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure SpreadsheetTransactionRecord entity
        modelBuilder.Entity<SpreadsheetTransactionRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            const int decPrec = 18;
            const int decScale = 4;
            entity.Property(e => e.Amount).HasPrecision(decPrec, decScale);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.SnapshotId);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => new { e.UserId, e.Category, e.Date });

            entity.HasOne(e => e.Snapshot)
                .WithMany(e => e.Transactions)
                .HasForeignKey(e => e.SnapshotId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Connection)
                .WithMany()
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Notification entity
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.SourceFeature).HasMaxLength(50);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
        });

        // --- Budget Tracker entities ---

        // Configure BudgetCategory entity — unique per user and kind among non-deleted rows
        modelBuilder.Entity<BudgetCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Kind).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.Kind, e.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        // Configure MonthlyBudgetPlan entity — one plan per user and calendar month among non-deleted rows
        modelBuilder.Entity<MonthlyBudgetPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => new { e.UserId, e.Year, e.Month })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasMany(e => e.Lines)
                .WithOne(e => e.Plan)
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure MonthlyBudgetLine entity — one line per plan and category among non-deleted rows
        modelBuilder.Entity<MonthlyBudgetLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.PlannedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ActualOverride).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.PlanId, e.CategoryId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BudgetTransaction entity — month membership is a date-range query on Date
        modelBuilder.Entity<BudgetTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => new { e.UserId, e.Date });

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure UserBudgetSourcePreference entity — one preference per user
        modelBuilder.Entity<UserBudgetSourcePreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Source).IsRequired();
            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });
    }
}

/// <summary>
/// Azure SQL-specific AppDbContext used for EF Core migrations under <c>Migrations/AzureSql</c>
/// and as the DI implementation when the active provider is Azure SQL.
/// </summary>
public sealed class AzureSqlAppDbContext : AppDbContext
{
    public AzureSqlAppDbContext(DbContextOptions<AzureSqlAppDbContext> options) : base(options)
    {
    }
}