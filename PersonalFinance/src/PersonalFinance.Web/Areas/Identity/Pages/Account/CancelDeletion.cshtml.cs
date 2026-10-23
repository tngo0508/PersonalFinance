using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.AccountDeletion;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

/// <summary>
/// The page the cancel link in the deletion email opens. Opening it changes nothing: email scanners follow links,
/// so only the button (a POST) cancels the Deletion Request. Anonymous, because the Account cannot sign in until
/// it is restored; the single-use token is the authorization.
/// </summary>
[AllowAnonymous]
public class CancelDeletionModel : PageModel
{
    private readonly IAccountDeletionApi _accountDeletionApi;
    private readonly ILogger<CancelDeletionModel> _logger;

    public CancelDeletionModel(IAccountDeletionApi accountDeletionApi, ILogger<CancelDeletionModel> logger)
    {
        _accountDeletionApi = accountDeletionApi;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    /// <summary>True once the Deletion Request was cancelled.</summary>
    public bool Cancelled { get; private set; }

    /// <summary>Why cancelling failed, safe to show; null when it did not fail.</summary>
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = AccountDeletionMessages.InvalidCancelLink;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = AccountDeletionMessages.InvalidCancelLink;
            return Page();
        }

        try
        {
            await _accountDeletionApi.CancelAsync(new CancelDeletionRequest { Token = Token },
                HttpContext.RequestAborted);
            Cancelled = true;
        }
        catch (Refit.ApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            ErrorMessage = AccountDeletionMessages.InvalidCancelLink;
        }
        catch (Exception ex) when ((ex is Refit.ApiException or HttpRequestException or TaskCanceledException)
                                   && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Cancelling a Deletion Request failed.");
            ErrorMessage = ApiErrorMessages.ForUser(ex, "The deletion could not be cancelled. Please try again.");
        }

        return Page();
    }
}
