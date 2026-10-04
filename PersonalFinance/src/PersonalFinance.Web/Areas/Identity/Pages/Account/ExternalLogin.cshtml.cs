using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly IUserEmailStore<IdentityUser> _emailStore;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IUserStore<IdentityUser> userStore,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ProviderDisplayName { get; set; }

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public IActionResult OnGet() => RedirectToPage("./Login");

    public IActionResult OnPost(string provider, string? returnUrl = null)
    {
        // Request a redirect to the external login provider.
        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");

        if (remoteError != null)
        {
            ErrorMessage = $"Error from external provider: {remoteError}";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Error loading external login information.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // Sign in the user with this external login provider if the user already has a login.
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (result.Succeeded)
        {
            _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                await SynchronizeCustomClaimsAsync(user, info.Principal);
                await _signInManager.RefreshSignInAsync(user);
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

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email))
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                if (existingUser.EmailConfirmed)
                {
                    var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                    if (addLoginResult.Succeeded)
                    {
                        _logger.LogInformation("Linked {LoginProvider} login to existing user {Email}.", info.LoginProvider, email);
                        await SynchronizeCustomClaimsAsync(existingUser, info.Principal);
                        await _signInManager.SignInAsync(existingUser, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }

                    foreach (var error in addLoginResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An account with this email address already exists but has not been confirmed. Please confirm your email address first.");
                }

                Input.Email = email;
                return Page();
            }

            // Create a new user automatically with confirmed email from OAuth provider
            var user = CreateUser();
            await _userStore.SetUserNameAsync(user, email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, email, CancellationToken.None);
            user.EmailConfirmed = true;

            var createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (addLoginResult.Succeeded)
                {
                    _logger.LogInformation("User created an account using {LoginProvider} provider.", info.LoginProvider);
                    await SynchronizeCustomClaimsAsync(user, info.Principal);
                    await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in addLoginResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

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
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Error loading external login information during confirmation.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        ProviderDisplayName = info.ProviderDisplayName;
        ReturnUrl = returnUrl;

        if (ModelState.IsValid)
        {
            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                if (existingUser.EmailConfirmed)
                {
                    var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                    if (addLoginResult.Succeeded)
                    {
                        _logger.LogInformation("Linked {LoginProvider} login to existing user {Email}.", info.LoginProvider, Input.Email);
                        await SynchronizeCustomClaimsAsync(existingUser, info.Principal);
                        await _signInManager.SignInAsync(existingUser, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }

                    foreach (var error in addLoginResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An account with this email address already exists but has not been confirmed. Please confirm your email address first.");
                }

                return Page();
            }

            var user = CreateUser();
            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            user.EmailConfirmed = true;

            var createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (addLoginResult.Succeeded)
                {
                    _logger.LogInformation("User created an account using {LoginProvider} provider.", info.LoginProvider);
                    await SynchronizeCustomClaimsAsync(user, info.Principal);
                    await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in addLoginResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
        }

        return Page();
    }

    private async Task SynchronizeCustomClaimsAsync(IdentityUser user, ClaimsPrincipal externalPrincipal)
    {
        var existingClaims = await _userManager.GetClaimsAsync(user);
        var claimsToAdd = new List<Claim>();
        var claimsToRemove = new List<Claim>();

        // 1. Profile Picture Claim
        var pictureUrl = externalPrincipal.FindFirstValue("urn:google:picture")
                         ?? externalPrincipal.FindFirstValue("picture");

        if (!string.IsNullOrWhiteSpace(pictureUrl))
        {
            var existingPictureClaim = existingClaims.FirstOrDefault(c => c.Type == "urn:google:picture");
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
        var givenName = externalPrincipal.FindFirstValue(ClaimTypes.GivenName)
                        ?? externalPrincipal.FindFirstValue(ClaimTypes.Name);

        if (!string.IsNullOrWhiteSpace(givenName))
        {
            var existingNameClaim = existingClaims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName);
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
        var locale = externalPrincipal.FindFirstValue("urn:google:locale")
                     ?? externalPrincipal.FindFirstValue("locale");

        if (!string.IsNullOrWhiteSpace(locale))
        {
            var existingLocaleClaim = existingClaims.FirstOrDefault(c => c.Type == "urn:google:locale");
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
