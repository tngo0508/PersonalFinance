using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.AccountLifecycle;

/// <summary>
/// A locked-out Account (for example one with a pending Deletion Request) cannot get back in through Google sign-in:
/// neither with a linked Google login nor by having one linked to it by email.
/// </summary>
public class ExternalLoginLockoutTests
{
    [Fact]
    public async Task GoogleSignIn_ToALockedAccountByEmail_IsRefused_AndLinksNothing()
    {
        await using ExternalLoginCallbackHarness harness = await ExternalLoginCallbackHarness.CreateAsync(
            new ExternalLoginCallbackHarness.Options { LockedOut = true, GoogleLinked = false });

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.Equal("./Lockout", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Empty(await harness.UserManager.GetLoginsAsync(harness.User!));
        Assert.Equal(0, harness.SignInManager.SignInCalls);
    }

    [Fact]
    public async Task GoogleSignIn_ToALockedAccountWithALinkedLogin_IsRefused()
    {
        await using ExternalLoginCallbackHarness harness = await ExternalLoginCallbackHarness.CreateAsync(
            new ExternalLoginCallbackHarness.Options { LockedOut = true, GoogleLinked = true });

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.Equal("./Lockout", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(0, harness.SignInManager.SignInCalls);
    }
}
