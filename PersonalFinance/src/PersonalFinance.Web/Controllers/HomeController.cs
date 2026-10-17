using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the root URL. Signed-out visitors see the public Landing Page;
/// signed-in users are redirected to their Dashboard.
/// </summary>
[AllowAnonymous]
public class HomeController : Controller
{
    /// <summary>
    /// Displays the Landing Page, or redirects signed-in users to the Dashboard.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(DashboardController.Index), "Dashboard");
        }

        return View(LandingPageViewModel.Create());
    }
}
