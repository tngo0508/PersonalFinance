using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Features.AccountLifecycle;

/// <summary>
/// EF Core implementation of <see cref="IAccountPurgeService"/>. Each Account is erased in one <c>SaveChanges</c>,
/// so one transaction: either everything about it goes, or nothing does. <see cref="DeletionRequest.Status"/> and the
/// Identity user's concurrency stamp are concurrency tokens, so a request cancelled (or purged elsewhere) meanwhile
/// makes the save fail and the Account is skipped.
/// </summary>
internal sealed class AccountPurgeService : IAccountPurgeService
{
    private static readonly MethodInfo RemoveRowsMethod =
        typeof(AccountPurgeService).GetMethod(nameof(RemoveRowsAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly AppDbContext _db;
    private readonly ILogger<AccountPurgeService> _logger;
    private readonly TimeProvider _timeProvider;

    public AccountPurgeService(AppDbContext db, TimeProvider timeProvider, ILogger<AccountPurgeService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AccountPurgeSummary> PurgeDueAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        List<int> dueIds = await DueRequestsQuery(now).ToListAsync(cancellationToken);
        int purged = 0, skipped = 0, failed = 0;

        foreach (int requestId in dueIds)
        {
            _db.ChangeTracker.Clear();
            try
            {
                if (await PurgeOneAsync(requestId, now, cancellationToken))
                {
                    purged++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                skipped++;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Deletion Request {DeletionRequestId} skipped: cancelled or purged by another run meanwhile.",
                        requestId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The request id only: never the Account's id, email or data.
                failed++;
                _logger.LogError(ex, "Purging Deletion Request {DeletionRequestId} failed; it is retried on the next run.",
                    requestId);
            }
        }

        _db.ChangeTracker.Clear();
        return new AccountPurgeSummary(purged, skipped, failed);
    }

    /// <summary>
    /// Pending requests whose Grace Period has ended, oldest first. Internal so tests can check it translates to
    /// Azure SQL.
    /// </summary>
    internal IQueryable<int> DueRequestsQuery(DateTime now) =>
        _db.DeletionRequests.AsNoTracking()
            .Where(r => r.Status == DeletionRequestStatus.Pending && r.PurgeAfter <= now)
            .OrderBy(r => r.PurgeAfter).ThenBy(r => r.Id)
            .Select(r => r.Id);

    /// <summary>
    /// The user-owned entity types the Purge hard-deletes: every <see cref="AuditableEntity"/> except Community stories
    /// and comments, which are anonymized instead. Internal so tests can check every new user-owned entity is covered.
    /// </summary>
    internal IReadOnlyList<Type> DeletedEntityTypes() =>
        _db.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => typeof(AuditableEntity).IsAssignableFrom(t)
                        && t != typeof(CommunityStory) && t != typeof(CommunityComment))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Erases one Account in a single save. Returns false when the request is no longer pending and due.
    /// </summary>
    private async Task<bool> PurgeOneAsync(int requestId, DateTime now, CancellationToken cancellationToken)
    {
        DeletionRequest? request = await _db.DeletionRequests.FirstOrDefaultAsync(r =>
            r.Id == requestId && r.Status == DeletionRequestStatus.Pending && r.PurgeAfter <= now, cancellationToken);
        if (request is null)
        {
            return false;
        }

        string userId = request.UserId;
        await AnonymizeCommunityContentAsync(userId, request.RequestedAt, cancellationToken);
        await RemoveCachedDriveFilesAsync(userId, cancellationToken);
        foreach (Type type in DeletedEntityTypes())
        {
            await (Task)RemoveRowsMethod.MakeGenericMethod(type).Invoke(this, [userId, cancellationToken])!;
        }

        await RemoveIdentityUserAsync(userId, cancellationToken);

        request.Status = DeletionRequestStatus.Purged;
        request.PurgedAt = now;
        request.UserId = DeletedAccount.UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The Account's stories and comments stay, written by "Deleted User" under the placeholder id, and the ones the
    /// Deletion Request hid are shown again. Ones the Account deleted itself are erased, with other people's likes on
    /// them: nobody can see them, so keeping them would only keep the Account's words.
    /// </summary>
    private async Task AnonymizeCommunityContentAsync(string userId, DateTime requestedAt,
        CancellationToken cancellationToken)
    {
        foreach (CommunityStory? story in await _db.CommunityStories.IgnoreQueryFilters()
                     .Where(s => s.UserId == userId).ToListAsync(cancellationToken))
        {
            if (DeletedByTheAccount(story, requestedAt))
            {
                _db.CommunityStoryLikes.RemoveRange(await _db.CommunityStoryLikes.IgnoreQueryFilters()
                    .Where(l => l.StoryId == story.Id).ToListAsync(cancellationToken));
                _db.CommunityStories.Remove(story);
                continue;
            }

            Anonymize(story, requestedAt);
            story.AuthorName = DeletedAccount.DisplayName;
        }

        foreach (CommunityComment? comment in await _db.CommunityComments.IgnoreQueryFilters()
                     .Where(c => c.UserId == userId).ToListAsync(cancellationToken))
        {
            if (DeletedByTheAccount(comment, requestedAt))
            {
                _db.CommunityCommentLikes.RemoveRange(await _db.CommunityCommentLikes.IgnoreQueryFilters()
                    .Where(l => l.CommentId == comment.Id).ToListAsync(cancellationToken));
                _db.CommunityComments.Remove(comment);
                continue;
            }

            Anonymize(comment, requestedAt);
            comment.AuthorName = DeletedAccount.DisplayName;
        }
    }

    /// <summary>
    /// Cached Google Drive file listings belong to the Account's connections but are not user-owned rows themselves.
    /// </summary>
    private async Task RemoveCachedDriveFilesAsync(string userId, CancellationToken cancellationToken)
    {
        List<int?> connectionIds = await _db.GoogleDriveConnections.IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .Select(c => (int?)c.Id)
            .ToListAsync(cancellationToken);
        _db.GoogleDriveCachedFiles.RemoveRange(await _db.GoogleDriveCachedFiles
            .Where(f => connectionIds.Contains(f.ConnectionId))
            .ToListAsync(cancellationToken));
    }

    private async Task RemoveRowsAsync<TEntity>(string userId, CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        _db.Set<TEntity>().RemoveRange(await _db.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Removes the Identity user with its roles, claims, logins and tokens. They are removed explicitly rather than
    /// left to the database's cascade, so the result is the same on every provider.
    /// </summary>
    private async Task RemoveIdentityUserAsync(string userId, CancellationToken cancellationToken)
    {
        _db.UserRoles.RemoveRange(await _db.UserRoles.Where(r => r.UserId == userId).ToListAsync(cancellationToken));
        _db.UserClaims.RemoveRange(await _db.UserClaims.Where(c => c.UserId == userId).ToListAsync(cancellationToken));
        _db.UserLogins.RemoveRange(await _db.UserLogins.Where(l => l.UserId == userId).ToListAsync(cancellationToken));
        _db.UserTokens.RemoveRange(await _db.UserTokens.Where(t => t.UserId == userId).ToListAsync(cancellationToken));

        IdentityUser? user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is not null)
        {
            _db.Users.Remove(user);
        }
    }

    /// <summary>Soft-deleted before the Deletion Request, by the Account itself.</summary>
    private static bool DeletedByTheAccount(AuditableEntity row, DateTime requestedAt) =>
        row.IsDeleted && row.DeletedAt != requestedAt;

    private static void Anonymize(AuditableEntity row, DateTime requestedAt)
    {
        row.UserId = DeletedAccount.UserId;
        if (row.IsDeleted && row.DeletedAt == requestedAt)
        {
            row.IsDeleted = false;
            row.DeletedAt = null;
        }
    }
}
