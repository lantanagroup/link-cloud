using Automation.UI.Models;
using Automation.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class RunsController(IAutomationRunManager runManager) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var runs = runManager.GetRuns();
        return View(runs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartScenarioRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index));

        var runId = await runManager.StartAsync(request, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = runId });
    }

    [HttpGet]
    public IActionResult Details(Guid id)
    {
        var run = runManager.GetRun(id);
        if (run == null)
            return NotFound();

        return View(run);
    }

    [HttpGet]
    public IActionResult Status(Guid id)
    {
        var run = runManager.GetRun(id);
        if (run == null)
            return NotFound();

        return Json(run);
    }
}
