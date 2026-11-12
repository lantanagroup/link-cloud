using Link.UI.Models;
using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Saved cap for concurrent report batches. Sits with the other Automation admin pages.
/// </summary>
[Route("Automation/ReportBatches")]
public sealed class ReportBatchSettingsController(IServiceProvider services) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Report batches";
        ViewData["AutomationSection"] = "batches";
        var store = services.GetService<IReportBatchSettingsStore>();
        if (store is null)
        {
            return View(new ReportBatchSettingsPage
            {
                MaxConcurrency = ReportBatchConcurrencyPolicy.DefaultMaxConcurrency,
                UpperBound = ReportBatchConcurrencyPolicy.UpperBound,
                Ready = false,
                Error = "Report batches are unavailable until the automation engine is ready."
            });
        }

        return View(new ReportBatchSettingsPage
        {
            MaxConcurrency = await store.GetMaxConcurrencyAsync(cancellationToken),
            UpperBound = ReportBatchConcurrencyPolicy.UpperBound,
            Ready = true,
            Message = TempData["BatchSettings"] as string,
            Error = TempData["BatchSettingsError"] as string
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int maxConcurrency, CancellationToken cancellationToken)
    {
        var store = services.GetService<IReportBatchSettingsStore>();
        if (store is null)
        {
            TempData["BatchSettingsError"] = "Report batches are unavailable until the automation engine is ready.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            TempData["BatchSettingsError"] = ReportBatchConcurrencyPolicy.Validate(0);
            return RedirectToAction(nameof(Index));
        }

        var error = ReportBatchConcurrencyPolicy.Validate(maxConcurrency);
        if (error != null)
        {
            TempData["BatchSettingsError"] = error;
            return RedirectToAction(nameof(Index));
        }

        await store.SaveMaxConcurrencyAsync(maxConcurrency, cancellationToken);
        TempData["BatchSettings"] = $"Saved. New batches use a cap of {maxConcurrency}.";
        return RedirectToAction(nameof(Index));
    }
}
