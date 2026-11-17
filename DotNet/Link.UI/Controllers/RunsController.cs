using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Token page for callers that GET /Runs and scrape __RequestVerificationToken.
/// The automation index itself lives at /Automation.
/// </summary>
public sealed class RunsController : Controller
{
    [HttpGet("/Runs")]
    public IActionResult Index() => View();
}
