using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Investment Calculator feature.
/// Provides a compound growth calculator for users to project investment returns.
/// This is a client-side calculator with no server-side persistence.
/// </summary>
[Authorize]
public class InvestmentCalculatorController : Controller
{
    /// <summary>
    /// Displays the Investment Calculator page with compound growth projection tools.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
