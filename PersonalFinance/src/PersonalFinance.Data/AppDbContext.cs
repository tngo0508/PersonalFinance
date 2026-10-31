using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;

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
    /// Community comments on polls or Strategies table set.
    /// </summary>
    public DbSet<CommunityComment> CommunityComments => Set<CommunityComment>();

    /// <summary>
    /// Community Reactions (user -> Strategy or comment, by kind) table set.
    /// </summary>
    public DbSet<CommunityReaction> CommunityReactions => Set<CommunityReaction>();

    /// <summary>
    /// Guild Hall Strategies table set: Pending until an Admin approves or rejects them.
    /// </summary>
    public DbSet<CommunityStrategy> CommunityStrategies => Set<CommunityStrategy>();

    /// <summary>
    /// Guild Hall Pseudonyms (one per Account) table set.
    /// </summary>
    public DbSet<CommunityPseudonym> CommunityPseudonyms => Set<CommunityPseudonym>();

    /// <summary>
    /// Guild Hall Challenges (written by Admins, shared) table set.
    /// </summary>
    public DbSet<CommunityChallenge> CommunityChallenges => Set<CommunityChallenge>();

    /// <summary>
    /// Guild Hall Challenge memberships (user -> Challenge) table set.
    /// </summary>
    public DbSet<CommunityChallengeMember> CommunityChallengeMembers => Set<CommunityChallengeMember>();

    /// <summary>
    /// Guild Hall Check-ins (user -> Challenge period, private) table set.
    /// </summary>
    public DbSet<CommunityCheckIn> CommunityCheckIns => Set<CommunityCheckIn>();

    /// <summary>
    /// User budget allocations table set.
    /// </summary>
    public DbSet<UserBudgetAllocation> UserBudgetAllocations => Set<UserBudgetAllocation>();

    /// <summary>
    /// User savings goals table set.
    /// </summary>
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();

    /// <summary>
    /// Contributions — dated deposits and withdrawals of a Savings Goal; the goal's saved amount is their total.
    /// </summary>
    public DbSet<Contribution> Contributions => Set<Contribution>();

    /// <summary>
    /// Investment Scenarios — saved, named sets of Investment Calculator inputs, never results (#592).
    /// </summary>
    public DbSet<InvestmentScenario> InvestmentScenarios => Set<InvestmentScenario>();

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
    /// An Account's display preferences: its Language (one row per Account).
    /// </summary>
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    /// <summary>
    /// User notifications table set — stores budget alerts, savings milestones, and sync status notifications.
    /// </summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>
    /// User consent records table set — tracks acceptance of Terms of Service and Privacy Policy.
    /// </summary>
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();

    /// <summary>
    /// AI assistant token usage records table set — tracks cost per request for quota enforcement.
    /// </summary>
    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();

    /// <summary>
    /// Feedback table set — Bug, Idea and Other messages Accounts send to the operator.
    /// </summary>
    public DbSet<Feedback> Feedback => Set<Feedback>();

    /// <summary>
    /// Deletion Requests — an Account's request to be erased, kept after the Purge as the audit record (ADR 0005).
    /// </summary>
    public DbSet<DeletionRequest> DeletionRequests => Set<DeletionRequest>();

    /// <summary>
    /// Research snapshots — one row per public market dataset, replaced wholesale by the Research refresh job.
    /// </summary>
    public DbSet<ResearchSnapshot> ResearchSnapshots => Set<ResearchSnapshot>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    /// <summary>
    /// Priority Walks — an Account's saved passes through the Money Priorities Priority Tree.
    /// </summary>
    public DbSet<PriorityWalk> PriorityWalks => Set<PriorityWalk>();

    /// <summary>
    /// Lesson completions — the Lessons of the Journey an Account has finished, one row each (the Atelier, #513).
    /// </summary>
    public DbSet<LessonCompletion> LessonCompletions => Set<LessonCompletion>();

    /// <summary>
    /// Review card states — what an Account last said about each Review flash card (the Atelier, #513).
    /// </summary>
    public DbSet<ReviewCardState> ReviewCardStates => Set<ReviewCardState>();

    /// <summary>
    /// ASP.NET Core Data Protection keys table set for preserving cookie/token encryption across restarts.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global query filters for soft deletes — automatically exclude soft-deleted records
        builder.Entity<NetWorthParameters>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SavingsGoal>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Contribution>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SavedArticle>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserBudgetAllocation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<GoogleDriveConnection>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SpreadsheetBudgetSnapshot>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SpreadsheetTransactionRecord>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CommunityStrategy>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CommunityComment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CommunityReaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CommunityPollVote>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CommunityPseudonym>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CommunityChallengeMember>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CommunityCheckIn>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BudgetCategory>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<MonthlyBudgetPlan>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<MonthlyBudgetLine>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BudgetTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserBudgetSourcePreference>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserPreference>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserConsent>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Feedback>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PriorityWalk>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LessonCompletion>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ReviewCardState>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<InvestmentScenario>().HasQueryFilter(e => !e.IsDeleted);

        // Configure AiUsageRecord entity — usage records are never soft-deleted
        builder.Entity<AiUsageRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EstimatedCostUsd).HasPrecision(18, 6);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Explicit Identity key/FK lengths so both providers (and design-time scaffolding) produce
        // bounded string columns. SQL Server cannot index or key nvarchar(max).
        const int identityKeyMaxLength = 450;
        const int identityLoginTokenMaxLength = 128;

        builder.Entity<IdentityUser>(entity => { entity.Property(e => e.Id).HasMaxLength(identityKeyMaxLength); });

        builder.Entity<IdentityRole>(entity => { entity.Property(e => e.Id).HasMaxLength(identityKeyMaxLength); });

        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.Property(e => e.LoginProvider).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.ProviderKey).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
        });

        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
            entity.Property(e => e.RoleId).HasMaxLength(identityKeyMaxLength);
        });

        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
            entity.Property(e => e.LoginProvider).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.Name).HasMaxLength(identityLoginTokenMaxLength);
        });

        builder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
        });

        builder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.Property(e => e.RoleId).HasMaxLength(identityKeyMaxLength);
        });


        // Configure GoogleDriveConnection entity
        builder.Entity<GoogleDriveConnection>(entity =>
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
            ConfigureCurrencyCode(entity);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.FolderId });

            entity.HasMany(e => e.CachedFiles)
                .WithOne(e => e.Connection)
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        // Configure GoogleDriveCachedFile entity
        builder.Entity<GoogleDriveCachedFile>(entity =>
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
        builder.Entity<NetWorthParameters>(entity =>
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
            ConfigureCurrencyCode(entity);
        });

        ConfigureCommunity(builder);

        // --- Personal Finance entities ---

        // Configure UserBudgetAllocation entity
        builder.Entity<UserBudgetAllocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Period).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => new { e.UserId, e.Period }).IsUnique();
            const int decPrec = 18;
            const int decScale = 2;
            entity.Property(e => e.MonthlyIncome).HasPrecision(decPrec, decScale);
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
            ConfigureCurrencyCode(entity);
        });

        // Configure SavingsGoal entity
        builder.Entity<SavingsGoal>(entity =>
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
            ConfigureCurrencyCode(entity);
        });

        // Configure Contribution entity — a Savings Goal's dated deposits and withdrawals
        builder.Entity<Contribution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(200);
            entity.Property(e => e.Kind).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.SavingsGoalId, e.Date });

            entity.HasOne(e => e.SavingsGoal)
                .WithMany()
                .HasForeignKey(e => e.SavingsGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure SavedArticle entity
        builder.Entity<SavedArticle>(entity =>
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
        builder.Entity<SpreadsheetBudgetSnapshot>(entity =>
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
            ConfigureCurrencyCode(entity);

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
        builder.Entity<SpreadsheetTransactionRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            const int decPrec = 18;
            const int decScale = 4;
            entity.Property(e => e.Amount).HasPrecision(decPrec, decScale);
            ConfigureCurrencyCode(entity);

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
        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.SourceFeature).HasMaxLength(50);
            entity.Property(e => e.TitleKey).HasMaxLength(100);
            entity.Property(e => e.MessageKey).HasMaxLength(100);
            entity.Property(e => e.Arguments).HasMaxLength(2000);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
        });

        // --- Budget Tracker entities ---

        // Configure BudgetCategory entity — unique per user and kind among non-deleted rows
        builder.Entity<BudgetCategory>(entity =>
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
        builder.Entity<MonthlyBudgetPlan>(entity =>
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
        builder.Entity<MonthlyBudgetLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.PlannedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ActualOverride).HasPrecision(18, 2);
            ConfigureCurrencyCode(entity);
            entity.HasIndex(e => new { e.PlanId, e.CategoryId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BudgetTransaction entity — month membership is a date-range query on Date
        builder.Entity<BudgetTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            ConfigureCurrencyCode(entity);
            entity.HasIndex(e => new { e.UserId, e.Date });

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure UserBudgetSourcePreference entity — one preference per user
        builder.Entity<UserBudgetSourcePreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Source).IsRequired();
            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        ConfigureUserPreference(builder);

        // Configure UserConsent entity
        builder.Entity<UserConsent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.PolicyVersion).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasIndex(e => e.UserId);
        });

        // Configure Feedback entity — the Admin inbox lists by status, newest first
        builder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.Message).IsRequired().HasMaxLength(FeedbackLimits.MessageMaxLength);
            entity.Property(e => e.PageUrl).HasMaxLength(FeedbackLimits.PageUrlMaxLength);
            entity.Property(e => e.Status).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
        });

        // Configure DeletionRequest entity — looked up by Account, by cancel token, and by the Purge (status, due date)
        builder.Entity<DeletionRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            // Concurrency token: a cancel and the Purge (or two cancels) cannot both act on the same pending request.
            entity.Property(e => e.Status).IsRequired().IsConcurrencyToken();
            entity.Property(e => e.CancelTokenHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CancelTokenHash).IsUnique();
            entity.HasIndex(e => new { e.Status, e.PurgeAfter });
        });

        ConfigureResearchSnapshot(builder);
        ConfigureExchangeRate(builder);
        ConfigurePriorityWalk(builder);
        ConfigureInvestmentScenario(builder);
        ConfigureJourneyProgress(builder);
    }

    /// <summary>
    /// Journey progress (the Atelier, #513): one Lesson completion per Account and Lesson, and one Review card state
    /// per Account and Term, each unique among non-deleted rows. Slugs are file names in the content folder
    /// (ADR 0019), so they are ASCII and short.
    /// </summary>
    private static void ConfigureJourneyProgress(ModelBuilder builder)
    {
        builder.Entity<LessonCompletion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.LessonSlug).IsRequired().HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.TimeZoneId).IsRequired().HasMaxLength(64).IsUnicode(false);
            entity.HasIndex(e => new { e.UserId, e.LessonSlug })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<ReviewCardState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.TermSlug).IsRequired().HasMaxLength(80).IsUnicode(false);
            entity.HasIndex(e => new { e.UserId, e.TermSlug })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });
    }

    /// <summary>
    /// A money-bearing row's <c>CurrencyCode</c> (ADR 0016): <c>char(3)</c>, required, <c>USD</c> by default so
    /// every row saved before the column existed is US dollars.
    /// </summary>
    private static void ConfigureCurrencyCode<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        entity.Property<string>("CurrencyCode")
            .IsRequired()
            .HasMaxLength(SupportedCurrencies.CodeLength)
            .IsUnicode(false)
            .IsFixedLength()
            .HasDefaultValue(SupportedCurrencies.DefaultCode);
    }

    /// <summary>
    /// The Community entities (the Guild Hall): Pseudonyms, polls, options and votes, Strategies, comments and Reactions.
    /// Strategies and comments store no author name: the author is the row's <c>UserId</c>, shown by its Pseudonym.
    /// </summary>
    private static void ConfigureCommunity(ModelBuilder builder)
    {
        // Configure CommunityPoll entity
        builder.Entity<CommunityPoll>(entity =>
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
        builder.Entity<CommunityPollOption>(entity =>
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
        builder.Entity<CommunityPollVote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => new { e.UserId, e.PollId }).IsUnique();
            entity.HasIndex(e => e.OptionId);
        });

        // Configure CommunityComment entity
        builder.Entity<CommunityComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(CommunityCommentLimits.TextMaxLength);
            entity.HasIndex(e => e.PollId);
            entity.HasIndex(e => e.StrategyId);
            entity.HasIndex(e => e.CreatedAtUtc);

            entity.HasOne(e => e.Poll)
                .WithMany()
                .HasForeignKey(e => e.PollId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Strategy)
                .WithMany()
                .HasForeignKey(e => e.StrategyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CommunityReaction entity — one live Reaction per Account, kind and target. Taking one back
        // soft-deletes it, so the unique indexes skip deleted rows and the target's counts read by target and kind.
        // The foreign keys do not cascade: the Purge removes the Reactions on what it erases.
        builder.Entity<CommunityReaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Kind).IsRequired();
            entity.HasIndex(e => new { e.StrategyId, e.Kind });
            entity.HasIndex(e => new { e.CommentId, e.Kind });
            entity.HasIndex(e => new { e.UserId, e.StrategyId, e.Kind })
                .IsUnique()
                .HasFilter("[StrategyId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(e => new { e.UserId, e.CommentId, e.Kind })
                .IsUnique()
                .HasFilter("[CommentId] IS NOT NULL AND [IsDeleted] = 0");
            entity.ToTable(t => t.HasCheckConstraint("CK_CommunityReactions_OneTarget",
                "([StrategyId] IS NULL AND [CommentId] IS NOT NULL) OR ([StrategyId] IS NOT NULL AND [CommentId] IS NULL)"));

            entity.HasOne(e => e.Strategy)
                .WithMany()
                .HasForeignKey(e => e.StrategyId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Comment)
                .WithMany()
                .HasForeignKey(e => e.CommentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure CommunityStrategy entity — the board reads Approved ones by category, newest first, plus the
        // viewer's own; the Admin queue reads by status, oldest first
        builder.Entity<CommunityStrategy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(CommunityStrategyLimits.CategoryMaxLength);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(CommunityStrategyLimits.TitleMaxLength);
            entity.Property(e => e.Tried).IsRequired().HasMaxLength(CommunityStrategyLimits.TextMaxLength);
            entity.Property(e => e.Happened).IsRequired().HasMaxLength(CommunityStrategyLimits.TextMaxLength);
            entity.Property(e => e.Keeping).IsRequired().HasMaxLength(CommunityStrategyLimits.TextMaxLength);
            entity.Property(e => e.Status).IsRequired();
            entity.HasIndex(e => new { e.Status, e.Category, e.CreatedAt });
            entity.HasIndex(e => e.UserId);
        });

        // Configure CommunityPseudonym entity — one row per Account, one name per row. Both indexes are unfiltered on
        // purpose: a re-roll replaces the name in place, so an Account never has a second row, and a soft-deleted row
        // (an Account in its Grace Period) keeps its name reserved until the Purge deletes the row.
        builder.Entity<CommunityPseudonym>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(60);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure CommunityChallenge entity — shared data written by Admins, never deleted; the Guild Hall reads the
        // ones not yet ended
        builder.Entity<CommunityChallenge>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(CommunityChallengeLimits.TitleMaxLength);
            entity.Property(e => e.Habit).IsRequired().HasMaxLength(CommunityChallengeLimits.HabitMaxLength);
            entity.Property(e => e.Period).IsRequired();
            entity.HasIndex(e => e.EndDate);
        });

        // Configure CommunityChallengeMember entity — one live membership per Account and Challenge
        builder.Entity<CommunityChallengeMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.TimeZoneId).IsRequired()
                .HasMaxLength(CommunityChallengeLimits.TimeZoneIdMaxLength).IsUnicode(false);
            entity.HasIndex(e => new { e.UserId, e.ChallengeId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => e.ChallengeId);

            entity.HasOne(e => e.Challenge)
                .WithMany()
                .HasForeignKey(e => e.ChallengeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure CommunityCheckIn entity — one live Check-in per Account, Challenge and period; the Guild Hall
        // counts a period's Check-ins by Challenge
        builder.Entity<CommunityCheckIn>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => new { e.UserId, e.ChallengeId, e.PeriodStart })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.ChallengeId, e.PeriodStart });

            entity.HasOne(e => e.Challenge)
                .WithMany()
                .HasForeignKey(e => e.ChallengeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// An Account's display preferences (its Language and Reporting Currency): one row per Account among non-deleted
    /// rows.
    /// </summary>
    private static void ConfigureUserPreference(ModelBuilder builder)
    {
        builder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Language).IsRequired().HasMaxLength(10);
            entity.Property(e => e.ReportingCurrency)
                .IsRequired()
                .HasMaxLength(SupportedCurrencies.CodeLength)
                .IsUnicode(false)
                .IsFixedLength()
                .HasDefaultValue(SupportedCurrencies.DefaultCode);
            entity.Property(e => e.ShowJourneyOnDashboard).IsRequired().HasDefaultValue(true);
            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });
    }

    /// <summary>
    /// PriorityWalk: the column table of the Money Priorities spec; Earlier results list an Account's newest first.
    /// </summary>
    private static void ConfigurePriorityWalk(ModelBuilder builder)
    {
        builder.Entity<PriorityWalk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.MonthlyAmount).HasPrecision(18, 2);
            entity.Property(e => e.Purpose).HasMaxLength(300);
            entity.Property(e => e.DebtApr).HasPrecision(5, 2);
            entity.Property(e => e.DebtBalance).HasPrecision(18, 2);
            entity.Property(e => e.EmergencyFundMonths).HasPrecision(4, 1);
            entity.Property(e => e.MonthlyExpenses).HasPrecision(18, 2);
            entity.Property(e => e.EmployerMatchPercent).HasPrecision(5, 2);
            entity.Property(e => e.AgeBand).HasMaxLength(16);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(200);
            entity.Property(e => e.LeafId).IsRequired().HasMaxLength(8);
            entity.Property(e => e.PurposeNote).HasMaxLength(2000);
            ConfigureCurrencyCode(entity);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        });
    }

    /// <summary>
    /// InvestmentScenario: inputs only, every amount in the row's currency (ADR 0016); the Saved panel lists an
    /// Account's scenarios, so they are indexed by Account.
    /// </summary>
    private static void ConfigureInvestmentScenario(ModelBuilder builder)
    {
        builder.Entity<InvestmentScenario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(InvestmentCalculatorLimits.ScenarioNameMaxLength);
            entity.Property(e => e.Note).HasMaxLength(InvestmentCalculatorLimits.ScenarioNoteMaxLength);
            entity.Property(e => e.Mode).IsRequired();
            entity.Property(e => e.InitialAmount).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyContribution).HasPrecision(18, 2);
            entity.Property(e => e.GoalTarget).HasPrecision(18, 2);
            entity.Property(e => e.AssumedReturn).HasPrecision(5, 2);
            entity.Property(e => e.ContributionIncrease).HasPrecision(5, 2);
            entity.Property(e => e.Inflation).HasPrecision(5, 2);
            entity.Property(e => e.Fee).HasPrecision(5, 2);
            ConfigureCurrencyCode(entity);
            entity.HasIndex(e => e.UserId);
        });
    }

    /// <summary>
    /// ResearchSnapshot: not user-owned, one row per dataset, read by its key.
    /// </summary>
    private static void ConfigureResearchSnapshot(ModelBuilder builder)
    {
        builder.Entity<ResearchSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Dataset).IsRequired().HasMaxLength(32);
            entity.Property(e => e.FirstPeriod).IsRequired().HasMaxLength(16);
            entity.Property(e => e.LastPeriod).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Payload).IsRequired();
            entity.HasIndex(e => e.Dataset).IsUnique();
        });
    }

    /// <summary>
    /// ExchangeRate: system data written by the daily rate refresh, one row per base, quote and day.
    /// </summary>
    private static void ConfigureExchangeRate(ModelBuilder builder)
    {
        builder.Entity<ExchangeRate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BaseCurrency)
                .IsRequired()
                .HasMaxLength(SupportedCurrencies.CodeLength)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.QuoteCurrency)
                .IsRequired()
                .HasMaxLength(SupportedCurrencies.CodeLength)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Rate).HasPrecision(18, 8);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(32);
            entity.HasIndex(e => new { e.BaseCurrency, e.QuoteCurrency, e.Date }).IsUnique();
        });
    }
}

/// <summary>
/// SQL Server AppDbContext used for EF Core migrations under <c>Migrations/AzureSql</c>
/// and as the DI implementation when the active provider is <see cref="DatabaseProviderType.AzureSql"/>.
/// One migration set serves both production Azure SQL and local SQL Server in development
/// (the Docker container from <c>compose.yaml</c>, a native instance or LocalDB).
/// </summary>
public sealed class AzureSqlAppDbContext : AppDbContext
{
    public AzureSqlAppDbContext(DbContextOptions<AzureSqlAppDbContext> options) : base(options)
    {
    }
}
