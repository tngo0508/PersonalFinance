using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// The Idle Sign-out check-in (#332): <c>js/core/idle-sign-out.js</c> posts here while the user is active, at most once
/// every 5 minutes, so someone typing for a long time without saving is not signed out. The cookie is renewed by
/// <see cref="IdleSignOutCookie"/>; the action itself does nothing. Never calls the ApiService.
/// </summary>
[Authorize]
public class IdleSignOutController : Controller
{
    /// <summary>Answers 204; a signed-out caller gets 401 from the cookie handler instead.</summary>
    [HttpPost(IdleSignOutOptions.CheckInPath)]
    [ValidateAntiForgeryToken]
    public IActionResult CheckIn() => NoContent();
}
