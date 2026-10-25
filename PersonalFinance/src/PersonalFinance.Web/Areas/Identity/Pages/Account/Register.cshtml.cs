using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly IUserEmailStore<IdentityUser> _emailStore;
    private readonly ILogger<RegisterModel> _logger;
    private readonly IAccountEmailService _accountEmails;
    private readonly IUserPreferenceService _preferences;
    private readonly IUserConsentService _consentService;

    public RegisterModel(
        UserManager<IdentityUser> userManager,
        IUserStore<IdentityUser> userStore,
        SignInManager<IdentityUser> signInManager,
        ILogger<RegisterModel> logger,
        IAccountEmailService accountEmails,
        IUserPreferenceService preferences,
        IUserConsentService consentService)
    {
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _signInManager = signInManager;
        _logger = logger;
        _accountEmails = accountEmails;
        _preferences = preferences;
        _consentService = consentService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

    public class InputModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.EmailAddress")]
        [Display(Name = "Identity.Field.Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, ErrorMessage = "Validation.StringLength.Range", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.ConfirmPassword")]
        [Compare("Password", ErrorMessage = "Identity.Validation.PasswordsDoNotMatch")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Identity.Field.AcceptTerms")]
        public bool AcceptTerms { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        // [Required] on a bool is always satisfied, so acceptance must be enforced explicitly.
        if (!Input.AcceptTerms)
        {
            ModelState.AddModelError("Input.AcceptTerms", "You must agree to the Terms of Service and Privacy Policy to create an account.");
        }

        if (ModelState.IsValid)
        {
            IdentityUser user = CreateUser();

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            IdentityResult result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password.");

                await _userManager.AssignDefaultRoleAsync(user, _logger);

                string userId = await _userManager.GetUserIdAsync(user);

                // Save consent record
                await _consentService.RecordConsentAsync(
                    userId!,
                    AppVersion.Current,
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                // The Language the visitor registered in becomes the Account's, so its emails use it from the first.
                await _preferences.SetLanguageAsync(userId, AppLanguages.Current().Code);

                string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                string? callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme);

                await _accountEmails.SendConfirmationAsync(user, Input.Email, callbackUrl ?? string.Empty);

                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                }
                else
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
            }
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
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
                $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
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
