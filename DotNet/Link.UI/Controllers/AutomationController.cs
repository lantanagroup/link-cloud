using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("Automation")]
public sealed class AutomationController : Controller
{
    private readonly AutomationRunReader _runs;

    public AutomationController(AutomationRunReader runs)
    {
        _runs = runs;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(AutomationRunQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Automation";
        ViewData["AutomationSection"] = "runs";
        return View(await _runs.LoadDashboardAsync(query, cancellationToken));
    }

    [HttpGet("data")]
    public async Task<IActionResult> Data(AutomationRunQuery query, CancellationToken cancellationToken)
    {
        return Json(await _runs.LoadDashboardAsync(query, cancellationToken));
    }

    [HttpGet("Runs/{id:guid}")]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Automation run";
        ViewData["AutomationSection"] = "runs";
        return View(await _runs.LoadRunAsync(id, cancellationToken));
    }

    [HttpGet("Runs/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        return Json(await _runs.LoadRunAsync(id, cancellationToken));
    }
}
