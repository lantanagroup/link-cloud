using Automation.UI.Models;
using Automation.UI.Services;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class RunsController(IAutomationRunManager runManager, IDataAcquisitionServiceClient dataAcqClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var runs = await runManager.GetRunsPageAsync(pageNumber, pageSize, cancellationToken);
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
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        return View(run);
    }

    [HttpGet]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        return Json(run);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await runManager.DeleteRunAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { pageNumber, pageSize });
    }

    [HttpGet]
    public async Task<IActionResult> PipelineSnapshot(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await runManager.GetPipelineSnapshotAsync(id, cancellationToken);
        if (snapshot == null)
            return NoContent();

        return Json(snapshot);
    }

    /// <summary>
    /// Proxy endpoint for DA log drill-down. Returns a paged list of DA logs
    /// for the run's facility/report so the detail modal can paginate.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DataAcquisitionLogs(
        Guid id, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        var facilityId = run.FacilityId;
        var reportId = run.ReportId;

        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(reportId))
            return Json(new { records = Array.Empty<object>(), metadata = new { totalCount = 0 } });

        var result = await dataAcqClient.SearchAcquisitionLogsAsync(
            facilityId, reportId, pageSize, pageNumber, cancellationToken);

        return Json(result);
    }
}
