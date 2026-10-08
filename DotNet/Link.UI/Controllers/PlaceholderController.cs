using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Old placeholder routes. Each one redirects to the section that replaced it.
/// </summary>
public sealed class PlaceholderController : Controller
{
    [HttpGet]
    public IActionResult Reports() => RedirectToAction("Index", "Reports");

    [HttpGet]
    public IActionResult Configuration() => RedirectToAction("Index", "Configuration");

    [HttpGet]
    public IActionResult Logs() => RedirectToAction("Index", "Logs");

    [HttpGet]
    public IActionResult System() => RedirectToAction("Index", "System");

    [HttpGet]
    public IActionResult Automation() => RedirectToAction("Index", "Automation");
}

