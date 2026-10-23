using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// Serves the public legal pages (privacy, terms, disclaimer, cookies, contact).
/// These pages must remain reachable without signing in, for example from the registration form.
/// </summary>
[AllowAnonymous]
public class LegalController : Controller
{
    [HttpGet("/privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("/terms")]
    public IActionResult Terms()
    {
        return View();
    }

    [HttpGet("/disclaimer")]
    public IActionResult Disclaimer()
    {
        return View();
    }

    [HttpGet("/cookies")]
    public IActionResult Cookies()
    {
        return View();
    }

    [HttpGet("/contact")]
    public IActionResult Contact()
    {
        return View();
    }
}
