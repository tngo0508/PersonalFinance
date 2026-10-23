using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Web.Features.Legal;

/// <summary>
/// Records and manages user consent to Terms of Service and Privacy Policy.
/// </summary>
public interface IUserConsentService
{
    /// <summary>
    /// Records a new consent entry for a user after registration.
    /// </summary>
    /// <param name="userId">The user's identity ID.</param>
    /// <param name="policyVersion">The version of the policies accepted.</param>
    /// <param name="ipAddress">Optional IP address at the time of consent.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task RecordConsentAsync(string userId, string policyVersion, string? ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IUserConsentService"/> that persists consent records via EF Core.
/// </summary>
public class UserConsentService : IUserConsentService
{
    private readonly AppDbContext _appDbContext;

    public UserConsentService(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task RecordConsentAsync(string userId, string policyVersion, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var consent = new UserConsent
        {
            UserId = userId,
            PolicyVersion = policyVersion,
            AcceptedTerms = true,
            AcceptedPrivacy = true,
            IpAddress = ipAddress
        };
        _appDbContext.UserConsents.Add(consent);
        await _appDbContext.SaveChangesAsync(cancellationToken);
    }
}
