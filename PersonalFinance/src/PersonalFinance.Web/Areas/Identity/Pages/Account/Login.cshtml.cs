using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;
    private readonly IApiServiceWarmer _apiServiceWarmer;
    private readonly IStringLocalizer _localizer;

    public LoginModel(SignInManager<IdentityUser> signInManager, ILogger<LoginModel> logger,
        IApiServiceWarmer apiServiceWarmer,
        IStringLocalizer localizer)
    {
        _signInManager = signInManager;
        _logger = logger;
        _apiServiceWarmer = apiServiceWarmer;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.EmailAddress")]
        [Display(Name = "Identity.Field.Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Validation.Required")]
        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Identity.Field.RememberMe")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        // Start ApiService while the user types, so the Dashboard after sign-in does not hit a cold container.
        _ = _apiServiceWarmer.WarmUpAsync();

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/");

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                return LocalRedirect(returnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty, _localizer["Account.Login.EmailConfirmationRequired"]);
                return Page();
            }
            else
            {
                ModelState.AddModelError(string.Empty, _localizer["Account.Login.InvalidAttempt"]);
                return Page();
            }
        }

        return Page();
    }
}
