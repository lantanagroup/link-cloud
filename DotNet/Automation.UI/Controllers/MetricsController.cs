using Automation.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class MetricsController(
    MetricsRunPresenter presenter,
    ILiveProcessUtilizationService liveUtilization) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await presenter.GetDashboardAsync(pageNumber, pageSize, cancellationToken);
        return View(dashboard);
    }

    [HttpGet]
    public async Task<IActionResult> Scenario(Guid id, CancellationToken cancellationToken)
    {
        var history = await presenter.GetScenarioHistoryAsync(id, cancellationToken);
        if (history == null)
            return NotFound();

        return View(history);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await presenter.GetDetailAsync(id, cancellationToken);
        if (detail == null)
            return NotFound();

        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Compare(Guid a, Guid b, CancellationToken cancellationToken)
    {
        var compare = await presenter.GetCompareAsync(a, b, cancellationToken);
        if (compare == null)
            return NotFound();

        return View(compare);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> LiveUtilization(CancellationToken cancellationToken)
    {
        var snapshot = await liveUtilization.GetAsync(cancellationToken);
        return Json(snapshot);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await presenter.DeleteSnapshotAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();

        return RedirectToAction(nameof(Index));
    }
}
