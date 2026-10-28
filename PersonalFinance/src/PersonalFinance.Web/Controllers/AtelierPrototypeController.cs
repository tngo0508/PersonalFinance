using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// PROTOTYPE (#496) - throwaway. Three structurally different Atelier looks on /AtelierPrototype?variant=A|B|C,
/// with stub content held in the view. Development only; nothing persists. Do not merge to main.
/// </summary>
[AllowAnonymous]
public sealed class AtelierPrototypeController : Controller
{
    private readonly IWebHostEnvironment _environment;

    public AtelierPrototypeController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Index([FromQuery] string? variant = null)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        string key = (variant ?? "A").ToUpperInvariant();
        return View("Index", key is "A" or "B" or "C" ? key : "A");
    }
}
