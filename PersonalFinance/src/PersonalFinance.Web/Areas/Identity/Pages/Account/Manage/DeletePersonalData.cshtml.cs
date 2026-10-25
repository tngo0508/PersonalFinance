using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.AccountDeletion;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Services;
using System.Globalization;
using System.Net;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Delete your account: makes a Deletion Request (ADR 0005) once the Account confirms it, with its password or, for
/// an Account without one (Google sign-in only), by typing <see cref="ConfirmationWord"/>. Then emails the cancel
/// link and signs out. Replaces the Identity UI default page, which deleted only the sign-in Account.
/// </summary>
[Authorize]
public class DeletePersonalDataModel : PageModel
{
    /// <summary>What an Account without a password types to confirm.</summary>
    public const string ConfirmationWord = "DELETE";

    private readonly IAccountDeletionApi _accountDeletionApi;
    private readonly IAccountEmailService _accountEmails;
    private readonly ILogger<DeletePersonalDataModel> _logger;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public DeletePersonalDataModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager,
        IAccountDeletionApi accountDeletionApi, IAccountEmailService accountEmails, ILogger<DeletePersonalDataModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _accountDeletionApi = accountDeletionApi;
        _accountEmails = accountEmails;
        _logger = logger;
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    /// <summary>Whether the Account confirms with its password (true) or by typing the confirmation word.</summary>
    public bool RequirePassword { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        RequirePassword = await _userManager.HasPasswordAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        RequirePassword = await _userManager.HasPasswordAsync(user);
        bool confirmed = RequirePassword
            ? !string.IsNullOrEmpty(Input.Password) && await _userManager.CheckPasswordAsync(user, Input.Password)
            : IsConfirmationWord(Input.Confirmation);
        if (!confirmed)
        {
            ModelState.AddModelError(string.Empty, RequirePassword
                ? "Incorrect password."
                : $"Type {ConfirmationWord} to confirm.");
            return Page();
        }

        DeletionRequestAcceptedDto accepted;
        try
        {
            accepted = await _accountDeletionApi.RequestAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex) when ((ex is Refit.ApiException or HttpRequestException or TaskCanceledException)
                                   && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            if (ex is not Refit.ApiException { StatusCode: HttpStatusCode.Conflict })
            {
                _logger.LogWarning(ex, "Making a Deletion Request failed.");
            }

            ModelState.AddModelError(string.Empty,
                ApiErrorMessages.ForUser(ex, "Your account could not be deleted. Please try again later."));
            return Page();
        }

        bool emailSent = await SendCancelLinkAsync(user, accepted);
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Account/DeletionRequested", new
        {
            area = "Identity",
            purgeAfter = accepted.PurgeAfterUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            emailSent
        });
    }

    /// <summary>
    /// True when <paramref name="typed"/> is the confirmation word, ignoring surrounding spaces but not case.
    /// </summary>
    public static bool IsConfirmationWord(string? typed) =>
        string.Equals(typed?.Trim(), ConfirmationWord, StringComparison.Ordinal);

    /// <summary>
    /// Emails the cancel link. The request already stands when this runs, so a failure is logged (never with the
    /// link) and reported to the user instead of undoing it.
    /// </summary>
    private async Task<bool> SendCancelLinkAsync(IdentityUser user, DeletionRequestAcceptedDto accepted)
    {
        if (string.IsNullOrEmpty(user.Email))
        {
            return false;
        }

        string? cancelUrl = Url.Page("/Account/CancelDeletion", pageHandler: null,
            values: new { area = "Identity", token = accepted.CancelToken }, protocol: Request.Scheme);
        try
        {
            await _accountEmails.SendDeletionScheduledAsync(user, user.Email, cancelUrl ?? string.Empty,
                accepted.PurgeAfterUtc);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning("Sending the deletion email failed: {ExceptionType}.", ex.GetType().Name);
            return false;
        }
    }

    public class InputModel
    {
        public string? Password { get; set; }

        public string? Confirmation { get; set; }
    }
}
