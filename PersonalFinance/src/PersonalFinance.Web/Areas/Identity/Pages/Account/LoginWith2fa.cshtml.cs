// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginWith2faModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<LoginWith2faModel> _logger;
    private readonly IStringLocalizer _localizer;

    public LoginWith2faModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        ILogger<LoginWith2faModel> logger,
        IStringLocalizer localizer)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(7, ErrorMessage = "Validation.StringLength.Range", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Identity.Field.AuthenticatorCode")]
        public string TwoFactorCode { get; set; }

        [Display(Name = "Identity.Field.RememberMachine")]
        public bool RememberMachine { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string returnUrl = null)
    {
        // Ensure the user has gone through the username & password screen first
        IdentityUser user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            throw new InvalidOperationException($"Unable to load two-factor authentication user.");
        }

        ReturnUrl = returnUrl;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        returnUrl = returnUrl ?? Url.Content("~/");

        IdentityUser user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            throw new InvalidOperationException($"Unable to load two-factor authentication user.");
        }

        string authenticatorCode = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);

        // Never persistent: the session ends when the browser closes (Idle Sign-out, #332).
        Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, isPersistent: false, Input.RememberMachine);

        string userId = await _userManager.GetUserIdAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation(LoggerEventIds.UserLoginWith2FA, "User logged in with 2fa.");
            return LocalRedirect(returnUrl);
        }
        else if (result.IsLockedOut)
        {
            _logger.LogWarning(LoggerEventIds.UserLockout, "User account locked out.");
            return RedirectToPage("./Lockout");
        }
        else
        {
            _logger.LogWarning(LoggerEventIds.InvalidAuthenticatorCode, "Invalid authenticator code entered.");
            ModelState.AddModelError(string.Empty, _localizer["Account.LoginWith2fa.InvalidCode"]);
            return Page();
        }
    }
}
