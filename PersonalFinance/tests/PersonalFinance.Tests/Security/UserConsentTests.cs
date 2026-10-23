using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Security;

public class UserConsentTests
{
    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Consent_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task RecordConsentAsync_PersistsAcceptedConsentRecord()
    {
        await using AppDbContext context = CreateContext();
        var service = new UserConsentService(context);

        await service.RecordConsentAsync("user-1", "2.1.0", "203.0.113.7");

        UserConsent consent = await context.UserConsents.SingleAsync();
        Assert.Equal("user-1", consent.UserId);
        Assert.Equal("2.1.0", consent.PolicyVersion);
        Assert.True(consent.AcceptedTerms);
        Assert.True(consent.AcceptedPrivacy);
        Assert.Equal("203.0.113.7", consent.IpAddress);
        Assert.False(consent.IsDeleted);
    }

    [Fact]
    public async Task RecordConsentAsync_AllowsMissingIpAddress()
    {
        await using AppDbContext context = CreateContext();
        var service = new UserConsentService(context);

        await service.RecordConsentAsync("user-2", "2.1.0", null);

        UserConsent consent = await context.UserConsents.SingleAsync();
        Assert.Null(consent.IpAddress);
    }

    [Fact]
    public void LegalController_IsAnonymouslyAccessible()
    {
        var attribute = Attribute.GetCustomAttribute(typeof(LegalController), typeof(AllowAnonymousAttribute));

        Assert.NotNull(attribute);
    }
}
