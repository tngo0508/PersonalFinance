using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Data;

/// <summary>
/// Provides extension methods for safe, automated EF Core schema migrations and data seeding on application startup or ACA job initialization.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Executes EF Core migrations with resilience/retry logic and seeds initial data if missing.
    /// </summary>
    public static async Task MigrateAndSeedDatabaseAsync(
        this IServiceProvider serviceProvider,
        ILogger? logger = null,
        int maxRetries = 5,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(3);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger ??= scope.ServiceProvider.GetService<ILogger<AppDbContext>>();

        var providerName = db.Database.ProviderName ?? "Unknown";
        var targetHost = db.Database.IsRelational() ? db.Database.GetDbConnection().DataSource : "(in-memory)";
        logger?.LogInformation(
            "Starting database migration and initialization for provider: {Provider} on target host: {TargetHost}...",
            providerName,
            string.IsNullOrWhiteSpace(targetHost) ? "(unspecified/localhost)" : targetHost);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!db.Database.IsRelational())
                {
                    await db.Database.EnsureCreatedAsync(cancellationToken);
                    logger?.LogInformation("Non-relational database schema initialized.");
                }
                else
                {
                    // Apply pending EF Core migrations
                    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                    if (pendingMigrations.Count > 0)
                    {
                        logger?.LogInformation(
                            "Applying {Count} pending migration(s): {Migrations}",
                            pendingMigrations.Count,
                            string.Join(", ", pendingMigrations));

                        await db.Database.MigrateAsync(cancellationToken);
                        logger?.LogInformation("Database schema successfully migrated.");
                    }
                    else
                    {
                        // Ensure database exists if no migrations are pending (e.g. up to date)
                        await db.Database.MigrateAsync(cancellationToken);
                        logger?.LogInformation("Database schema is up to date (no pending migrations).");
                    }
                }

                // Seed initial community data if missing
                await SeedCommunityDataAsync(db, logger, cancellationToken);

                logger?.LogInformation("Database migration and initialization completed successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning(
                    ex,
                    "Database migration attempt {Attempt} of {MaxRetries} failed. Retrying in {DelaySeconds}s (waiting for Serverless/Database ready state)...",
                    attempt,
                    maxRetries,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30));
            }
        }
    }

    /// <summary>
    /// Executes database migration and seeding for an IHost / WebApplication instance.
    /// </summary>
    public static async Task MigrateAndSeedDatabaseAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        var logger = host.Services.GetService<ILoggerFactory>()?.CreateLogger("DatabaseInitializer");
        await host.Services.MigrateAndSeedDatabaseAsync(logger, cancellationToken: cancellationToken);
    }


    /// <summary>
    /// Seeds initial community polls and options if they don't exist.
    /// </summary>
    private static async Task SeedCommunityDataAsync(
        AppDbContext db,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (await db.CommunityPolls.AnyAsync(cancellationToken))
        {
            return;
        }

        logger?.LogInformation("Seeding initial community polls...");

        var now = DateTime.UtcNow;

        var polls = new List<CommunityPoll>
        {
            new CommunityPoll
            {
                PollCode = "poll-budget-method",
                Question = "What budgeting method works best for you?",
                Description = "We've all tried different approaches to managing money. Which one actually sticks?",
                Category = "Budgeting",
                IsActive = true,
                CreatedAtUtc = now.AddDays(-30),
                Options = new List<CommunityPollOption>
                {
                    new() { Text = "50/30/20 Rule", Order = 1 },
                    new() { Text = "Zero-Based Budgeting", Order = 2 },
                    new() { Text = "Envelope System", Order = 3 },
                    new() { Text = "Pay Yourself First", Order = 4 },
                    new() { Text = "I just wing it (no budget)", Order = 5 },
                },
            },
            new CommunityPoll
            {
                PollCode = "poll-savings-goal",
                Question = "What's your #1 financial goal this year?",
                Description = "Whether it's paying off debt or building an emergency fund — what's your priority?",
                Category = "Goals",
                IsActive = true,
                CreatedAtUtc = now.AddDays(-20),
                Options = new List<CommunityPollOption>
                {
                    new() { Text = "Build an emergency fund", Order = 1 },
                    new() { Text = "Pay off high-interest debt", Order = 2 },
                    new() { Text = "Start investing", Order = 3 },
                    new() { Text = "Save for a big purchase", Order = 4 },
                },
            },
            new CommunityPoll
            {
                PollCode = "poll-coffee-challenge",
                Question = "Have you ever tried the 'Coffee Challenge' (save your daily coffee money)?",
                Description = "The idea: skip the $5 latte and invest it instead. Did it actually work for you?",
                Category = "Experiments",
                IsActive = true,
                CreatedAtUtc = now.AddDays(-10),
                Options = new List<CommunityPollOption>
                {
                    new() { Text = "Yes, and it changed my habits", Order = 1 },
                    new() { Text = "Tried it, but couldn't stick with it", Order = 2 },
                    new() { Text = "I make coffee at home anyway", Order = 3 },
                    new() { Text = "Never tried it", Order = 4 },
                },
            },
        };

        await db.CommunityPolls.AddRangeAsync(polls, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("Seeded {Count} initial community polls with options.", polls.Count);

        // Seed initial community stories
        await SeedCommunityStoriesAsync(db, logger, cancellationToken);
    }

    /// <summary>
    /// Seeds initial community stories if they don't exist.
    /// </summary>
    private static async Task SeedCommunityStoriesAsync(
        AppDbContext db,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (await db.CommunityStories.AnyAsync(cancellationToken))
        {
            return;
        }

        logger?.LogInformation("Seeding initial community stories...");

        var now = DateTime.UtcNow;

        var stories = new List<CommunityStory>
        {
            new CommunityStory
            {
                UserId = "system",
                AuthorName = "Sarah & Mike, Austin TX",
                Title = "How We Cut Our Grocery Bill by 40% in 3 Months",
                Summary = "A couple shares their real experiment with meal planning, store brands, and the 'no restaurant' challenge — and what actually worked.",
                Body = "We started tracking every grocery purchase in January. The first week was eye-opening: we were spending $180/week on groceries and another $60 on snacks and impulse buys. Here's what changed:\n\nMeal planning Sundays — We sit down every Sunday and plan meals for the week. This alone cut waste by half.\n\nStore brands only — We switched to generic brands for staples like rice, pasta, and canned goods. The taste difference? Nearly zero.\n\nThe one-restaurant rule — Instead of cutting dining out entirely (which we couldn't sustain), we limited it to one meal per week.\n\nAfter 3 months, our average weekly spend dropped to $108 — a 40% reduction. The meal planning was the biggest factor, but the store brand switch added up more than we expected.",
                Category = "Budgeting",
                Likes = 24,
                CommentCount = 47,
                IsVisible = true,
                IsFeatured = true,
                PublishedAtUtc = now.AddDays(-45),
                ReadTimeMinutes = 4,
            },
            new CommunityStory
            {
                UserId = "system",
                AuthorName = "James R., Portland OR",
                Title = "I Paid Off $15K Credit Card Debt Using the Avalanche Method",
                Summary = "A step-by-step account of how one person tackled credit card debt — including the psychological hurdles and unexpected wins along the way.",
                Body = "When I started, I had three credit cards: $8K at 24% APR, $4K at 19%, and $3K at 15%. The avalanche method meant attacking the highest interest rate first while making minimum payments on the rest.\n\nMonth 1-4: Felt like nothing was happening. I was throwing $600/month at the $8K card and only $200 was going toward principal. Wanted to quit.\n\nMonth 5: The $8K card dropped below $7K. Momentum kicked in.\n\nMonth 8: First card paid off! Now I could redirect that $600 to the next card.\n\nMonth 14: All three cards paid off. Total interest paid: ~$3,200. Had I used the snowball method, I would've paid ~$3,800 in interest.\n\nThe hardest part wasn't the math — it was the first 4 months when progress felt invisible. If you're starting this journey, just hang in there.",
                Category = "Debt",
                Likes = 56,
                CommentCount = 83,
                IsVisible = true,
                IsFeatured = true,
                PublishedAtUtc = now.AddDays(-60),
                ReadTimeMinutes = 5,
            },
            new CommunityStory
            {
                UserId = "system",
                AuthorName = "Priya K., Chicago IL",
                Title = "The No-Spend Month Experiment: What I Learned",
                Summary = "One person tried spending zero money on non-essentials for 30 days. Here's what broke, what worked, and what they'd do differently.",
                Body = "I challenged myself to a no-spend month in July. Rules: only essentials (groceries, utilities, transport) were allowed. No eating out, no entertainment purchases, no impulse buys.\n\nWeek 1: Easy. I had a list of free activities and cooked at home. Felt productive.\n\nWeek 2: The itch started. I wanted to buy a book, go to a movie, grab coffee with a friend. Had to get creative with free alternatives.\n\nWeek 3: A friend's birthday came up. I made a homemade gift instead. It was actually more meaningful.\n\nWeek 4: Realization hit — I was spending so much out of habit, not need. I tracked every 'almost bought' moment.\n\nTotal saved: $680. But the real win was realizing how much of my spending was automatic. I didn't maintain the extreme approach, but I did keep a '24-hour rule' for any non-essential purchase over $20.",
                Category = "Experiments",
                Likes = 38,
                CommentCount = 61,
                IsVisible = true,
                IsFeatured = false,
                PublishedAtUtc = now.AddDays(-30),
                ReadTimeMinutes = 4,
            },
        };

        await db.CommunityStories.AddRangeAsync(stories, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("Seeded {Count} initial community stories.", stories.Count);
    }
}
