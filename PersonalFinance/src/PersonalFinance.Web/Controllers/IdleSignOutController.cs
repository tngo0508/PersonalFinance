using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// The Idle Sign-out check-in (#332): <c>js/core/idle-sign-out.js</c> posts here while the user is active, at most once
/// every 5 minutes, so someone typing for a long time without saving is not signed out. The cookie is renewed by
/// <see cref="Infrastructure.IdleSignOutCookie"/>; the action itself does nothing. Never calls the ApiService.
/// </summary>
[Authorize]
[Route("idle-sign-out")]
public class IdleSignOutController : Controller
{
    public const string CheckInPath = "/idle-sign-out/check-in";

    /// <summary>Answers 204; a signed-out caller gets 401 from the cookie handler instead.</summary>
    [HttpPost("check-in")]
    [ValidateAntiForgeryToken]
    public IActionResult CheckIn() => NoContent();
}
