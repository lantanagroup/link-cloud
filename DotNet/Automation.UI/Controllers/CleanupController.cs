using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class CleanupController(
    ILeftoverRunCleanup leftoverRunCleanup,
    ICleanupSettingsStore settingsStore,
    TimeProvider time,
    ILogger<CleanupController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetEffectiveAsync(cancellationToken);
        var now = time.GetUtcNow();
        var vm = new CleanupPageViewModel
        {
            Settings = settings,
            NowUtc = now,
            LastQuiesceAt = leftoverRunCleanup.LastQuiesceAt,
            LastQuiesceResult = leftoverRunCleanup.LastQuiesceResult is { } quiesce
                ? $"quiesced {quiesce.QuiescedFacilityIds.Count}/{quiesce.QuiesceCandidateCount}"
                : null,
            FromDate = now.UtcDateTime.Date.AddDays(-settings.TeardownRetention.TotalDays),
            ToDate = now.UtcDateTime.Date,
            CurrentActivity = leftoverRunCleanup.CurrentActivity
        };
        return View(vm);
    }

    [HttpGet]
    public IActionResult Progress()
        => Json(leftoverRunCleanup.CurrentActivity);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(CleanupSettingsForm form, string? runKind, CancellationToken cancellationToken)
    {
        var current = await settingsStore.GetEffectiveAsync(cancellationToken);
        var settings = ApplyForm(form, current);
        await settingsStore.SaveAsync(settings, cancellationToken);

        if (string.IsNullOrWhiteSpace(runKind))
            return Finish("Cleanup schedule and retention settings saved. The sweeper picks them up within 30 seconds.", started: false);

        return StartRun(runKind);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunCustomRange(CleanupCustomRangeForm form, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(form.FromDate.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(form.ToDate.Date, DateTimeKind.Utc).AddDays(1);
        if (to <= from)
            return Finish("Custom range To date must be on or after From date.", started: false, error: true);

        if (!form.TeardownFacilities && !form.PurgeHistory)
            return Finish("Choose leftover facility teardown and/or run-history purge for the custom range.", started: false, error: true);

        await Task.CompletedTask;
        return StartRun("custom-range", () => leftoverRunCleanup.StartCustomRangeInBackground(
            from, to, form.TeardownFacilities, form.PurgeHistory));
    }

    private IActionResult StartRun(string runKind, Action? start = null)
    {
        if (leftoverRunCleanup.IsRunning)
            return Finish("A cleanup pass is already running. Watch the activity panel; you can start another when it finishes.", started: false, error: true);

        try
        {
            if (start is not null)
            {
                start();
            }
            else
            {
                switch (runKind)
                {
                    case "quiesce":
                        leftoverRunCleanup.StartQuiesceInBackground();
                        break;
                    case "teardown":
                        leftoverRunCleanup.StartTeardownInBackground();
                        break;
                    case "history-purge":
                        leftoverRunCleanup.StartHistoryPurgeInBackground();
                        break;
                    default:
                        return Finish($"Unknown cleanup type '{runKind}'.", started: false, error: true);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start leftover cleanup {RunKind}.", runKind);
            return Finish($"Could not start cleanup: {ex.Message}", started: false, error: true);
        }

        var label = runKind switch
        {
            "quiesce" => "Quiesce leftover hot work",
            "teardown" => "Off-hours leftover teardown",
            "history-purge" => "Weekly history purge",
            "custom-range" => "Custom range cleanup",
            _ => "Cleanup"
        };
        return Finish($"{label} started. Progress updates live on this page.", started: true, mode: runKind);
    }

    private IActionResult Finish(string message, bool started, bool error = false, string? mode = null)
    {
        if (WantsJson())
        {
            if (error)
                return Conflict(new { error = message, started, mode, activity = leftoverRunCleanup.CurrentActivity });
            return Json(new { message, started, mode, activity = leftoverRunCleanup.CurrentActivity });
        }

        TempData[error ? "CleanupError" : "Cleanup"] = message;
        return RedirectToAction(nameof(Index));
    }

    private bool WantsJson()
        => Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
           || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    internal static LeftoverRunCleanupSettings ApplyForm(CleanupSettingsForm form, LeftoverRunCleanupSettings current)
        => new()
        {
            Enabled = form.Enabled,
            QuiesceEnabled = form.QuiesceEnabled,
            QuiesceInterval = TimeSpan.FromMinutes(Math.Clamp(form.QuiesceIntervalMinutes, 1, 24 * 60)),
            QuiesceGrace = TimeSpan.FromMinutes(Math.Clamp(form.QuiesceGraceMinutes, 0, 24 * 60)),
            TeardownRetention = TimeSpan.FromDays(Math.Clamp(form.TeardownRetentionDays, 1, 365)),
            AbortTtl = TimeSpan.FromDays(Math.Clamp(form.AbortTtlDays, 1, 365)),
            MaxFacilitiesPerPass = Math.Clamp(form.MaxFacilitiesPerPass, 1, 500),
            DailyTeardownEnabled = form.DailyTeardownEnabled,
            DailyTeardownTimeUtc = CleanupSchedule.ParseTimeUtc(form.DailyTeardownTimeUtc, current.DailyTeardownTimeUtc),
            WeeklyHistoryPurgeEnabled = form.WeeklyHistoryPurgeEnabled,
            WeeklyHistoryPurgeDay = form.WeeklyHistoryPurgeDay,
            WeeklyHistoryPurgeTimeUtc = CleanupSchedule.ParseTimeUtc(form.WeeklyHistoryPurgeTimeUtc, current.WeeklyHistoryPurgeTimeUtc),
            CatchUpWindow = TimeSpan.FromHours(Math.Clamp(form.CatchUpWindowHours, 1, 12))
        };
}
