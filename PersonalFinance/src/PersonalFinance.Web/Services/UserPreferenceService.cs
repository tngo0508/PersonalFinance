using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Services;

/// <summary>
/// Reads and writes an Account's stored Language (localization spec §2.3). The row is the source of truth; the culture
/// cookie only mirrors it.
/// </summary>
public interface IUserPreferenceService
{
    /// <summary>The Account's stored Language code, or <c>null</c> when it has never chosen one.</summary>
    Task<string?> GetLanguageAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="language"/> for the Account, creating its preference row if needed.</summary>
    /// <exception cref="ArgumentException">The language is not supported.</exception>
    Task SetLanguageAsync(string userId, string language, CancellationToken cancellationToken = default);
}

/// <summary>EF Core implementation of <see cref="IUserPreferenceService"/>: one <see cref="UserPreference"/> per Account.</summary>
internal sealed class UserPreferenceService : IUserPreferenceService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public UserPreferenceService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<string?> GetLanguageAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        string? stored = await _db.UserPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .Select(p => p.Language)
            .FirstOrDefaultAsync(cancellationToken);
        // A language removed from the supported list later reads as no choice, so the request culture applies.
        return AppLanguages.Find(stored)?.Code;
    }

    public async Task SetLanguageAsync(string userId, string language, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        AppLanguage chosen = AppLanguages.Find(language)
                             ?? throw new ArgumentException($"Unsupported language '{language}'.", nameof(language));

        UserPreference? preference = await _db.UserPreferences
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (preference is null)
        {
            _db.UserPreferences.Add(new UserPreference { UserId = userId, Language = chosen.Code, CreatedAt = now });
        }
        else if (preference.Language != chosen.Code)
        {
            preference.Language = chosen.Code;
            preference.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
