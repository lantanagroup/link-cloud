using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Placeholder sections for left-nav groups not yet ported (phase 1).
/// </summary>
public sealed class PlaceholderController : Controller
{
    [HttpGet]
    public IActionResult Reports() => ComingSoon("Reports");

    [HttpGet]
    public IActionResult Configuration() => RedirectToAction("Index", "Configuration");

    [HttpGet]
    public IActionResult Logs() => RedirectToAction("Index", "Logs");

    [HttpGet]
    public IActionResult System() => RedirectToAction("Index", "System");

    [HttpGet]
    public IActionResult Automation() => RedirectToAction("Index", "Automation");

    private IActionResult ComingSoon(string section)
    {
        ViewData["Title"] = section;
        ViewData["Section"] = section;
        return View("~/Views/Shared/ComingSoon.cshtml");
    }
}

