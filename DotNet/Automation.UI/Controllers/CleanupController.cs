using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class CleanupController(
    LeftoverRunCleanupService leftoverRunCleanup,
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
            ToDate = now.UtcDateTime.Date
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(CleanupSettingsForm form, CancellationToken cancellationToken)
    {
        var current = await settingsStore.GetEffectiveAsync(cancellationToken);
        var settings = new LeftoverRunCleanupSettings
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

        await settingsStore.SaveAsync(settings, cancellationToken);
        TempData["Cleanup"] = "Cleanup schedule and retention settings saved. The sweeper picks them up within 30 seconds.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunQuiesce(CancellationToken cancellationToken)
        => await RunAsync(
            "Quiesce leftover hot work",
            () => leftoverRunCleanup.RunQuiesceNowAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunTeardown(CancellationToken cancellationToken)
        => await RunAsync(
            "Off-hours leftover teardown",
            () => leftoverRunCleanup.RunTeardownNowAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunHistoryPurge(CancellationToken cancellationToken)
        => await RunAsync(
            "Weekly history purge",
            () => leftoverRunCleanup.RunHistoryPurgeNowAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunCustomRange(CleanupCustomRangeForm form, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(form.FromDate.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(form.ToDate.Date, DateTimeKind.Utc).AddDays(1);
        if (to <= from)
        {
            TempData["CleanupError"] = "Custom range To date must be on or after From date.";
            return RedirectToAction(nameof(Index));
        }

        if (!form.TeardownFacilities && !form.PurgeHistory)
        {
            TempData["CleanupError"] = "Choose leftover facility teardown and/or run-history purge for the custom range.";
            return RedirectToAction(nameof(Index));
        }

        return await RunAsync(
            $"Custom range {from:yyyy-MM-dd} to {form.ToDate:yyyy-MM-dd} UTC",
            () => leftoverRunCleanup.RunCustomRangeAsync(
                from, to, form.TeardownFacilities, form.PurgeHistory, cancellationToken));
    }

    private async Task<IActionResult> RunAsync(string label, Func<Task<LeftoverCleanupResult>> action)
    {
        try
        {
            var result = await action();
            TempData["Cleanup"] = Format(label, result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Label} failed.", label);
            TempData["CleanupError"] = $"{label} failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    private static string Format(string label, LeftoverCleanupResult result)
    {
        var message = $"{label}: stopped {result.QuiescedFacilityIds.Count} of {result.QuiesceCandidateCount} leftover facilit{(result.QuiesceCandidateCount == 1 ? "y" : "ies")}, torn down {result.TornDownFacilityIds.Count}, purged {result.PurgedRunIds.Count} run record{(result.PurgedRunIds.Count == 1 ? "" : "s")}.";
        if (result.FailedFacilityIds.Count > 0)
            message += $" Failed facilities: {string.Join(", ", result.FailedFacilityIds)}.";
        if (result.FailedRunIds.Count > 0)
            message += $" Failed runs: {string.Join(", ", result.FailedRunIds)}.";
        if (result.QuiesceCandidateCount == 0 && result.TeardownCandidateCount == 0 && result.HistoryPurgeCandidateCount == 0)
            message = $"{label}: nothing matched.";
        return message;
    }
}
