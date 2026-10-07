using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("Automation/Configurations")]
public sealed class AutomationConfigurationsController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Automation configurations";
        ViewData["AutomationSection"] = "configurations";
        return View("~/Views/Automation/Configurations.cshtml");
    }
}
