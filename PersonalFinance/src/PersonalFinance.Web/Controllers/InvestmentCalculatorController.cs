using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Investment Calculator feature.
/// Provides a compound growth calculator for users to project investment returns.
/// This is a client-side calculator with no server-side persistence.
/// </summary>
[Authorize]
public class InvestmentCalculatorController(IWebHostEnvironment environment) : Controller
{
    /// <summary>
    /// Displays the Investment Calculator page with compound growth projection tools.
    /// PROTOTYPE (#587, throwaway branch): ?variant=A|B|C renders the UI prototype, Development only.
    /// </summary>
    [HttpGet]
    public IActionResult Index(string? variant)
    {
        if (environment.IsDevelopment() && !string.IsNullOrEmpty(variant))
        {
            return View("Prototype");
        }

        return View();
    }
}
