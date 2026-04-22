using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Runs");
}
