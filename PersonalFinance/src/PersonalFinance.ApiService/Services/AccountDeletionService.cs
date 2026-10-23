using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// EF Core implementation of <see cref="IAccountDeletionService"/>. Works on the Identity tables directly through
/// <see cref="AppDbContext"/>, so hiding the data, locking the Account and storing the request are one save.
/// </summary>
public class AccountDeletionService : IAccountDeletionService
{
    /// <summary>How long a Deletion Request can be cancelled before the Purge.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(30);

    private const int CancelTokenBytes = 32;

    private static readonly MethodInfo HideRowsMethod =
        typeof(AccountDeletionService).GetMethod(nameof(HideRowsAsync),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo RestoreRowsMethod =
        typeof(AccountDeletionService).GetMethod(nameof(RestoreRowsAsync),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AccountDeletionService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<DeletionRequestResult> RequestAsync(string userId, CancellationToken cancellationToken = default)
    {
        // The last-Admin check and the request must not interleave with another Admin leaving (ADR 0004).
        await AdminRoleLock.Instance.WaitAsync(cancellationToken);
        try
        {
            return await RequestCoreAsync(userId, cancellationToken);
        }
        finally
        {
            AdminRoleLock.Instance.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var now = UtcNow();
        var request = await _db.DeletionRequests.FirstOrDefaultAsync(r =>
            r.CancelTokenHash == tokenHash
            && r.Status == DeletionRequestStatus.Pending
            && r.PurgeAfter > now, cancellationToken);
        if (request is null)
        {
            return false;
        }

        foreach (var type in HiddenEntityTypes())
        {
            await InvokeAsync(RestoreRowsMethod, type, request.UserId, request.RequestedAt, cancellationToken);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is not null)
        {
            user.LockoutEnd = null;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        request.Status = DeletionRequestStatus.Cancelled;
        request.CancelledAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The user-owned entity types a Deletion Request hides and a cancel restores: every <see cref="AuditableEntity"/>
    /// except <see cref="AiUsageRecord"/>, the AI spend ledger that is never soft-deleted and that the Purge handles.
    /// Internal so tests can check every new user-owned entity is covered.
    /// </summary>
    internal IReadOnlyList<Type> HiddenEntityTypes() =>
        _db.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => typeof(AuditableEntity).IsAssignableFrom(t) && t != typeof(AiUsageRecord))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private async Task<DeletionRequestResult> RequestCoreAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return DeletionRequestResult.Refused(DeletionRequestOutcome.AccountNotFound);
        }

        if (await _db.DeletionRequests.AnyAsync(
                r => r.UserId == userId && r.Status == DeletionRequestStatus.Pending, cancellationToken))
        {
            return DeletionRequestResult.Refused(DeletionRequestOutcome.AlreadyPending);
        }

        if (await IsLastAdminAsync(userId, cancellationToken))
        {
            return DeletionRequestResult.Refused(DeletionRequestOutcome.LastAdmin);
        }

        // Whole milliseconds, so the stamp round-trips exactly through any datetime2 precision.
        var now = UtcNow();
        var stamp = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        foreach (var type in HiddenEntityTypes())
        {
            await InvokeAsync(HideRowsMethod, type, userId, stamp, cancellationToken);
        }

        // Lockout also stops external (Google) sign-in; the new security stamp ends other signed-in sessions at
        // their next validation.
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(CancelTokenBytes));
        var purgeAfter = stamp + GracePeriod;
        _db.DeletionRequests.Add(new DeletionRequest
        {
            UserId = userId,
            RequestedAt = stamp,
            PurgeAfter = purgeAfter,
            CancelTokenHash = HashToken(token)
        });

        await _db.SaveChangesAsync(cancellationToken);
        return DeletionRequestResult.Accepted(token, purgeAfter);
    }

    /// <summary>
    /// True when <paramref name="userId"/> holds the Admin Role and no other Admin remains outside a pending
    /// Deletion Request.
    /// </summary>
    private async Task<bool> IsLastAdminAsync(string userId, CancellationToken cancellationToken)
    {
        var adminIds = await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.Name == AppRoles.Admin
                select userRole.UserId)
            .ToListAsync(cancellationToken);
        if (!adminIds.Contains(userId))
        {
            return false;
        }

        var leavingIds = await _db.DeletionRequests.AsNoTracking()
            .Where(r => r.Status == DeletionRequestStatus.Pending)
            .Select(r => r.UserId)
            .ToListAsync(cancellationToken);
        return !adminIds.Any(id => id != userId && !leavingIds.Contains(id));
    }

    private async Task HideRowsAsync<TEntity>(string userId, DateTime stamp, CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var rows = await _db.Set<TEntity>()
            .Where(e => e.UserId == userId && !e.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.IsDeleted = true;
            row.DeletedAt = stamp;
        }
    }

    private async Task RestoreRowsAsync<TEntity>(string userId, DateTime stamp, CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var rows = await _db.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.IsDeleted && e.DeletedAt == stamp)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.IsDeleted = false;
            row.DeletedAt = null;
        }
    }

    private Task InvokeAsync(MethodInfo method, Type entityType, string userId, DateTime stamp,
        CancellationToken cancellationToken) =>
        (Task)method.MakeGenericMethod(entityType).Invoke(this, [userId, stamp, cancellationToken])!;

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
