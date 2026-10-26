using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ResendEmailConfirmationModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAccountEmailService _accountEmails;
    private readonly ILogger<ResendEmailConfirmationModel> _logger;
    private readonly IStringLocalizer _localizer;

    public ResendEmailConfirmationModel(
        UserManager<IdentityUser> userManager,
        IAccountEmailService accountEmails,
        ILogger<ResendEmailConfirmationModel> logger,
        IStringLocalizer localizer)
    {
        _userManager = userManager;
        _accountEmails = accountEmails;
        _logger = logger;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.EmailAddress")]
        [Display(Name = "Identity.Field.Email")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        IdentityUser? user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, _localizer["Account.Common.VerificationSent"]);
            return Page();
        }

        string userId = await _userManager.GetUserIdAsync(user);
        string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        string? callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { userId = userId, code = code },
            protocol: Request.Scheme);

        await _accountEmails.SendConfirmationAsync(user, Input.Email, callbackUrl ?? string.Empty);

        ModelState.AddModelError(string.Empty, _localizer["Account.Common.VerificationSent"]);
        return Page();
    }
}
