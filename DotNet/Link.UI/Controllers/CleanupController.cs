using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed class CleanupController : Controller
{
    private readonly ILeftoverRunCleanup? _leftoverRunCleanup;
    private readonly ICleanupSettingsStore? _settingsStore;
    private readonly ICleanupReportStore? _reportStore;
    private readonly LinkAutomationEngineStatus _engine;
    private readonly TimeProvider _time;
    private readonly ILogger<CleanupController> _logger;

    public CleanupController(
        IServiceProvider services,
        LinkAutomationEngineStatus engine,
        ILogger<CleanupController> logger)
    {
        _leftoverRunCleanup = services.GetService<ILeftoverRunCleanup>();
        _settingsStore = services.GetService<ICleanupSettingsStore>();
        _reportStore = services.GetService<ICleanupReportStore>();
        _engine = engine;
        _time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        _logger = logger;
    }

    private bool Ready => _engine.Ready && _leftoverRunCleanup is not null && _settingsStore is not null && _reportStore is not null;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Cleanup";
        ViewData["AutomationSection"] = "cleanup";
        if (!Ready)
        {
            ViewBag.EngineMessage = _engine.Message ?? AutomationRunReader.NotConfiguredMessage;
            var nowUnavailable = _time.GetUtcNow();
            return View(new CleanupPageViewModel
            {
                NowUtc = nowUnavailable,
                FromDate = nowUnavailable.UtcDateTime.Date.AddDays(-14),
                ToDate = nowUnavailable.UtcDateTime.Date,
                CurrentActivity = CleanupActivity.Idle
            });
        }

        var settings = await _settingsStore!.GetEffectiveAsync(cancellationToken);
        var now = _time.GetUtcNow();
        var vm = new CleanupPageViewModel
        {
            Settings = settings,
            NowUtc = now,
            LastQuiesceAt = _leftoverRunCleanup!.LastQuiesceAt,
            LastQuiesceResult = _leftoverRunCleanup.LastQuiesceResult is { } quiesce
                ? $"quiesced {quiesce.QuiescedFacilityIds.Count}/{quiesce.QuiesceCandidateCount}"
                : null,
            FromDate = now.UtcDateTime.Date.AddDays(-settings.TeardownRetention.TotalDays),
            ToDate = now.UtcDateTime.Date,
            CurrentActivity = _leftoverRunCleanup.CurrentActivity,
            RecentReports = await _reportStore!.ListRecentAsync(25, cancellationToken)
        };
        return View(vm);
    }

    [HttpGet]
    public IActionResult Progress()
        => Json(_leftoverRunCleanup?.CurrentActivity ?? CleanupActivity.Idle);

    [HttpGet]
    public async Task<IActionResult> Report(Guid id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id == Guid.Empty)
            return BadRequest("Invalid Id format");

        if (_reportStore is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var report = await _reportStore.GetAsync(id, cancellationToken);
        if (report == null)
            return NotFound();
        return Json(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(CleanupSettingsForm form, string? runKind, CancellationToken cancellationToken)
    {
        if (!Ready)
            return Finish(_engine.Message ?? AutomationRunReader.NotConfiguredMessage, started: false, error: true);

        var current = await _settingsStore!.GetEffectiveAsync(cancellationToken);
        var settings = ApplyForm(form, current);
        await _settingsStore.SaveAsync(settings, cancellationToken);

        if (string.IsNullOrWhiteSpace(runKind))
            return Finish("Cleanup schedule and retention settings saved. The sweeper picks them up within 30 seconds.", started: false);

        return StartRun(runKind);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RunCustomRange(CleanupCustomRangeForm form, CancellationToken cancellationToken)
    {
        if (!Ready)
            return Task.FromResult(Finish(_engine.Message ?? AutomationRunReader.NotConfiguredMessage, started: false, error: true));

        var from = DateTime.SpecifyKind(form.FromDate.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(form.ToDate.Date, DateTimeKind.Utc).AddDays(1);
        if (to <= from)
            return Task.FromResult(Finish("Custom range To date must be on or after From date.", started: false, error: true));

        if (!form.TeardownFacilities && !form.PurgeHistory)
            return Task.FromResult(Finish("Choose leftover facility teardown and/or run-history purge for the custom range.", started: false, error: true));

        return Task.FromResult(StartRun("custom-range", () => _leftoverRunCleanup!.StartCustomRangeInBackground(
            from, to, form.TeardownFacilities, form.PurgeHistory)));
    }

    private IActionResult StartRun(string runKind, Action? start = null)
    {
        if (_leftoverRunCleanup!.IsRunning)
            return Finish("A cleanup pass is already running. Watch the activity panel; you can start another when it finishes.", started: false, error: true);

        var kind = KnownRunKind(runKind);
        if (kind is null)
            return Finish("Unknown cleanup type.", started: false, error: true);

        try
        {
            if (start is not null)
            {
                start();
            }
            else
            {
                switch (kind)
                {
                    case "quiesce":
                        _leftoverRunCleanup.StartQuiesceInBackground();
                        break;
                    case "teardown":
                        _leftoverRunCleanup.StartTeardownInBackground();
                        break;
                    case "history-purge":
                        _leftoverRunCleanup.StartHistoryPurgeInBackground();
                        break;
                    default:
                        return Finish("Unknown cleanup type.", started: false, error: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start leftover cleanup {RunKind}.",
                kind.Replace("\r", string.Empty).Replace("\n", string.Empty).SanitizeForLog());
            return Finish($"Could not start cleanup: {ex.Message}", started: false, error: true);
        }

        var label = kind switch
        {
            "quiesce" => "Quiesce leftover hot work",
            "teardown" => "Off-hours leftover teardown",
            "history-purge" => "Weekly history purge",
            "custom-range" => "Custom range cleanup",
            _ => "Cleanup"
        };
        return Finish($"{label} started. Progress updates live on this page.", started: true, mode: kind);
    }

    private static string? KnownRunKind(string runKind) => runKind switch
    {
        "quiesce" => "quiesce",
        "teardown" => "teardown",
        "history-purge" => "history-purge",
        "custom-range" => "custom-range",
        _ => null
    };

    private IActionResult Finish(string message, bool started, bool error = false, string? mode = null)
    {
        var activity = _leftoverRunCleanup?.CurrentActivity ?? CleanupActivity.Idle;
        if (WantsJson())
        {
            if (error)
                return Conflict(new { error = message, started, mode, activity });
            return Json(new { message, started, mode, activity });
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
