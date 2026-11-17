using Link.UI.Models;
using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// One launch's live rows, grouped by EHR. Each EHR is still its own batch.
/// </summary>
[Route("Automation/ReportBatches/Launches")]
public sealed class ReportBatchLaunchTrackingController(IServiceProvider services) : Controller
{
    [HttpGet("{launchId:guid}")]
    public async Task<IActionResult> Detail(Guid launchId, CancellationToken cancellationToken)
    {
        ViewData["AutomationSection"] = "batches";
        var store = services.GetService<IReportBatchStore>();
        if (store is null)
        {
            ViewData["Title"] = "Report launch";
            return View(Unavailable());
        }

        var batches = await store.ListByLaunchAsync(launchId, cancellationToken);
        var launch = ReportBatchDisplay.FromLaunch(launchId, batches);
        if (launch is null)
        {
            ViewData["Title"] = "Launch not found";
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View(Missing(launchId));
        }

        ViewData["Title"] = "Report launch";
        return View(Found(launch));
    }

    [HttpGet("{launchId:guid}/status")]
    public async Task<IActionResult> Status(Guid launchId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var store = services.GetService<IReportBatchStore>();
        if (store is null)
            return Json(Unavailable());

        var batches = await store.ListByLaunchAsync(launchId, cancellationToken);
        var launch = ReportBatchDisplay.FromLaunch(launchId, batches);
        if (launch is null)
            return NotFound(Missing(launchId));

        return Json(Found(launch));
    }

    [HttpPost("{launchId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid launchId, CancellationToken cancellationToken)
    {
        var store = services.GetService<IReportBatchStore>();
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        if (store is null || dispatcher is null)
        {
            TempData["LaunchTrackingError"] = ReportBatchDisplay.UnavailableMessage;
            return RedirectToAction(nameof(Detail), new { launchId });
        }

        if ((await store.ListByLaunchAsync(launchId, cancellationToken)).Count == 0)
            return NotFound();

        try
        {
            await dispatcher.CancelLaunchAsync(launchId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["LaunchTrackingError"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { launchId });
    }

    [HttpPost("{launchId:guid}/batches/{batchId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBatch(Guid launchId, Guid batchId, CancellationToken cancellationToken)
    {
        var store = services.GetService<IReportBatchStore>();
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        if (store is null || dispatcher is null)
        {
            TempData["LaunchTrackingError"] = ReportBatchDisplay.UnavailableMessage;
            return RedirectToAction(nameof(Detail), new { launchId });
        }

        var batches = await store.ListByLaunchAsync(launchId, cancellationToken);
        if (batches.All(batch => batch.BatchId != batchId))
            return NotFound();

        await dispatcher.CancelBatchAsync(batchId, cancellationToken);
        return RedirectToAction(nameof(Detail), new { launchId });
    }

    private static ReportBatchLaunchSummaryPage Unavailable()
        => new()
        {
            Ready = false,
            Found = false,
            Message = ReportBatchDisplay.UnavailableMessage
        };

    private static ReportBatchLaunchSummaryPage Missing(Guid launchId)
        => new()
        {
            Ready = true,
            Found = false,
            Message = ReportBatchDisplay.MissingLaunchMessage(launchId)
        };

    private static ReportBatchLaunchSummaryPage Found(ReportBatchLaunchSummary launch)
        => new()
        {
            Ready = true,
            Found = true,
            Launch = launch
        };
}
