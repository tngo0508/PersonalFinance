using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.Web.Controllers;

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
