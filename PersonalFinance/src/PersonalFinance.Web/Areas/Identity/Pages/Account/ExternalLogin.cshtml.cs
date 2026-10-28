using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly IUserEmailStore<IdentityUser> _emailStore;
    private readonly ILogger<ExternalLoginModel> _logger;
    private readonly IAccountEmailService _accountEmails;
    private readonly IUserPreferenceService _preferences;
    private readonly IUserConsentService _consentService;
    private readonly IStringLocalizer _localizer;

    public ExternalLoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IUserStore<IdentityUser> userStore,
        ILogger<ExternalLoginModel> logger,
        IAccountEmailService accountEmails,
        IUserPreferenceService preferences,
        IUserConsentService consentService,
        IStringLocalizer localizer)
    {
        _accountEmails = accountEmails;
        _preferences = preferences;
        _consentService = consentService;
        _signInManager = signInManager;
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _logger = logger;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ProviderDisplayName { get; set; }

    /// <summary>Whether the provider supplied the email, so the form shows it read-only and asks only for consent.</summary>
    public bool EmailFromProvider { get; set; }

    /// <summary>Whether an Account the callback could not link already holds the provider's email.</summary>
    public bool ProviderEmailHasAccount { get; set; }

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.EmailAddress")]
        [Display(Name = "Identity.Field.Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Identity.Field.AcceptTerms")]
        public bool AcceptTerms { get; set; }
    }

    public IActionResult OnGet() => RedirectToPage("./Login");

    public IActionResult OnPost(string provider, string? returnUrl = null)
    {
        // Request a redirect to the external login provider.
        string? redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
        AuthenticationProperties properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");

        if (remoteError != null)
        {
            ErrorMessage = _localizer.Named("Account.ExternalLogin.ProviderError", ("remoteError", remoteError));
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        ExternalLoginInfo? info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = _localizer["Account.ExternalLogin.LoadError"];
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // Sign in the user with this external login provider if the user already has a login.
        Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (result.Succeeded)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
            }
            IdentityUser? user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                await SynchronizeCustomClaimsAsync(user, info.Principal);
                await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
            }
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }

        // If the user does not have an account or login linked, check if user exists with the provider email.
        ReturnUrl = returnUrl;
        ProviderDisplayName = info.ProviderDisplayName;

        string? email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email))
        {
            IdentityUser? existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                // SignInAsync below skips the lockout check that ExternalLoginSignInAsync makes, so a locked-out
                // Account (e.g. one with a pending Deletion Request) must be stopped here, before a login is linked.
                if (await _userManager.IsLockedOutAsync(existingUser))
                {
                    return RedirectToPage("./Lockout");
                }

                if (existingUser.EmailConfirmed)
                {
                    IdentityResult addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                    if (addLoginResult.Succeeded)
                    {
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation("Linked {LoginProvider} login to existing user {Email}.", info.LoginProvider, email);
                        }
                        await SynchronizeCustomClaimsAsync(existingUser, info.Principal);
                        await _signInManager.SignInAsync(existingUser, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }

                    foreach (IdentityError error in addLoginResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, _localizer["Account.ExternalLogin.UnconfirmedAccountExists"]);
                }

                EmailFromProvider = true;
                ProviderEmailHasAccount = true;
                Input.Email = email;
                return Page();
            }

            // A new Google user must agree to the terms before the Account exists, so the confirmation page asks
            // for that; the Account is created on its POST.
            EmailFromProvider = true;
            Input.Email = email;
            return Page();
        }

        // If email claim is not available from provider, prompt the user for email
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmationAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        // Get the information about the user from the external login provider
        ExternalLoginInfo? info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = _localizer["Account.ExternalLogin.LoadErrorConfirmation"];
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        ProviderDisplayName = info.ProviderDisplayName;
        ReturnUrl = returnUrl;

        // The trusted email comes only from the provider's sign-in, never from the posted form, so editing the
        // read-only field cannot confirm an address the visitor does not own.
        string? providerEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
        EmailFromProvider = !string.IsNullOrEmpty(providerEmail);
        if (EmailFromProvider)
        {
            ModelState.Remove($"{nameof(Input)}.{nameof(Input.Email)}");
            Input.Email = providerEmail!;
        }

        // [Required] on a bool is always satisfied, so acceptance must be enforced explicitly.
        if (!Input.AcceptTerms)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.AcceptTerms)}", _localizer["Account.Register.AcceptTermsRequired"]);
        }

        if (ModelState.IsValid)
        {
            // Linking happens only in the callback: an Account that appeared since then is refused, never linked.
            IdentityUser? existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                ProviderEmailHasAccount = EmailFromProvider;
                ModelState.AddModelError(string.Empty, _localizer["Account.ExternalLogin.AccountExists"]);
                return Page();
            }

            // A typed email is never trusted, so it stays unconfirmed until the visitor confirms it.
            IdentityUser user = CreateUser();
            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            user.EmailConfirmed = EmailFromProvider;

            IdentityResult createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                IdentityResult addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (addLoginResult.Succeeded)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("User created an account using {LoginProvider} provider.", info.LoginProvider);
                    }
                    await _userManager.AssignDefaultRoleAsync(user, _logger);

                    string userId = await _userManager.GetUserIdAsync(user);
                    await _consentService.RecordConsentAsync(
                        userId,
                        AppVersion.Current,
                        HttpContext.Connection.RemoteIpAddress?.ToString());

                    // The Language the visitor signed up in becomes the Account's (localization spec §2.3).
                    await _preferences.SetLanguageAsync(userId, AppLanguages.Current().Code);

                    if (EmailFromProvider)
                    {
                        await SynchronizeCustomClaimsAsync(user, info.Principal);
                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }

                    string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    string? callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId, code, returnUrl },
                        protocol: Request.Scheme);

                    await _accountEmails.SendConfirmationAsync(user, Input.Email, callbackUrl ?? string.Empty);

                    return RedirectToPage("./RegisterConfirmation", new { email = Input.Email, returnUrl });
                }

                foreach (IdentityError error in addLoginResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else
            {
                foreach (IdentityError error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
        }

        return Page();
    }

    private async Task SynchronizeCustomClaimsAsync(IdentityUser user, ClaimsPrincipal externalPrincipal)
    {
        IList<Claim> existingClaims = await _userManager.GetClaimsAsync(user);
        var claimsToAdd = new List<Claim>();
        var claimsToRemove = new List<Claim>();

        // 1. Profile Picture Claim
        string? pictureUrl = externalPrincipal.FindFirstValue("urn:google:picture")
                         ?? externalPrincipal.FindFirstValue("picture");

        if (!string.IsNullOrWhiteSpace(pictureUrl))
        {
            Claim? existingPictureClaim = existingClaims.FirstOrDefault(c => c.Type == "urn:google:picture");
            if (existingPictureClaim != null)
            {
                if (existingPictureClaim.Value != pictureUrl)
                {
                    claimsToRemove.Add(existingPictureClaim);
                    claimsToAdd.Add(new Claim("urn:google:picture", pictureUrl));
                }
            }
            else
            {
                claimsToAdd.Add(new Claim("urn:google:picture", pictureUrl));
            }
        }

        // 2. Given Name / Display Name Claim
        string? givenName = externalPrincipal.FindFirstValue(ClaimTypes.GivenName)
                        ?? externalPrincipal.FindFirstValue(ClaimTypes.Name);

        if (!string.IsNullOrWhiteSpace(givenName))
        {
            Claim? existingNameClaim = existingClaims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName);
            if (existingNameClaim != null)
            {
                if (existingNameClaim.Value != givenName)
                {
                    claimsToRemove.Add(existingNameClaim);
                    claimsToAdd.Add(new Claim(ClaimTypes.GivenName, givenName));
                }
            }
            else
            {
                claimsToAdd.Add(new Claim(ClaimTypes.GivenName, givenName));
            }
        }

        // 3. Locale Claim
        string? locale = externalPrincipal.FindFirstValue("urn:google:locale")
                     ?? externalPrincipal.FindFirstValue("locale");

        if (!string.IsNullOrWhiteSpace(locale))
        {
            Claim? existingLocaleClaim = existingClaims.FirstOrDefault(c => c.Type == "urn:google:locale");
            if (existingLocaleClaim != null)
            {
                if (existingLocaleClaim.Value != locale)
                {
                    claimsToRemove.Add(existingLocaleClaim);
                    claimsToAdd.Add(new Claim("urn:google:locale", locale));
                }
            }
            else
            {
                claimsToAdd.Add(new Claim("urn:google:locale", locale));
            }
        }

        if (claimsToRemove.Count > 0)
        {
            await _userManager.RemoveClaimsAsync(user, claimsToRemove);
        }

        if (claimsToAdd.Count > 0)
        {
            await _userManager.AddClaimsAsync(user, claimsToAdd);
        }
    }

    private IdentityUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<IdentityUser>();
        }
        catch
        {
            throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
        }
    }

    private IUserEmailStore<IdentityUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }
        return (IUserEmailStore<IdentityUser>)_userStore;
    }
}
