using Link.UI.Models;
using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// One batch's live rows, and the list used to open a batch again.
/// The launch form and the concurrency cap live on other pages.
/// </summary>
[Route("Automation/ReportBatches")]
public sealed class ReportBatchTrackingController(IServiceProvider services) : Controller
{
    [HttpGet("History")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Recent batches";
        ViewData["AutomationSection"] = "batches";
        var store = services.GetService<IReportBatchStore>();
        if (store is null)
        {
            return View(new ReportBatchHistoryPage
            {
                Ready = false,
                Message = ReportBatchDisplay.UnavailableMessage
            });
        }

        var batches = await store.ListRecentAsync(ReportBatchDisplay.HistoryLimit, cancellationToken);
        return View(new ReportBatchHistoryPage
        {
            Ready = true,
            Groups = ReportBatchDisplay.History(batches)
        });
    }

    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> Detail(Guid batchId, CancellationToken cancellationToken)
    {
        ViewData["AutomationSection"] = "batches";
        var store = services.GetService<IReportBatchStore>();
        if (store is null)
        {
            ViewData["Title"] = "Report batch";
            return View(Unavailable());
        }

        var batch = await store.GetAsync(batchId, cancellationToken);
        if (batch is null)
        {
            ViewData["Title"] = "Batch not found";
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View(Missing(batchId));
        }

        ViewData["Title"] = "Report batch";
        return View(Found(batch));
    }

    [HttpGet("{batchId:guid}/status")]
    public async Task<IActionResult> Status(Guid batchId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var store = services.GetService<IReportBatchStore>();
        if (store is null)
            return Json(Unavailable());

        var batch = await store.GetAsync(batchId, cancellationToken);
        if (batch is null)
            return NotFound(Missing(batchId));

        return Json(Found(batch));
    }

    [HttpPost("{batchId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid batchId, CancellationToken cancellationToken)
    {
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        if (dispatcher is null)
        {
            TempData["BatchTrackingError"] = ReportBatchDisplay.UnavailableMessage;
            return RedirectToAction(nameof(Detail), new { batchId });
        }

        var batch = await dispatcher.CancelBatchAsync(batchId, cancellationToken);
        if (batch is null)
            return NotFound();

        return RedirectToAction(nameof(Detail), new { batchId });
    }

    private static ReportBatchTrackingPage Unavailable()
        => new()
        {
            Ready = false,
            Found = false,
            Message = ReportBatchDisplay.UnavailableMessage
        };

    private static ReportBatchTrackingPage Missing(Guid batchId)
        => new()
        {
            Ready = true,
            Found = false,
            Message = ReportBatchDisplay.MissingMessage(batchId)
        };

    private static ReportBatchTrackingPage Found(ReportBatchDocument batch)
        => new()
        {
            Ready = true,
            Found = true,
            Batch = ReportBatchDisplay.From(batch)
        };
}
