using Automation.UI.Models.Metrics;
using Automation.UI.Services;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed class MetricsController : Controller
{
    private readonly LinkAutomationEngineStatus _engine;
    private readonly MetricsRunPresenter? _presenter;
    private readonly ILiveProcessUtilizationService? _liveUtilization;

    public MetricsController(LinkAutomationEngineStatus engine, IServiceProvider services)
    {
        _engine = engine;
        _presenter = services.GetService<MetricsRunPresenter>();
        _liveUtilization = services.GetService<ILiveProcessUtilizationService>();
    }

    private bool Ready => _engine.Ready && _presenter is not null;

    [HttpGet]
    public async Task<IActionResult> Index(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Performance";
        ViewData["AutomationSection"] = "metrics";
        if (!Ready)
        {
            ViewBag.EngineMessage = _engine.Message ?? AutomationRunReader.NotConfiguredMessage;
            return View(new MetricsDashboardViewModel());
        }

        var dashboard = await _presenter!.GetDashboardAsync(pageNumber, pageSize, cancellationToken);
        return View(dashboard);
    }

    [HttpGet]
    public async Task<IActionResult> Scenario(Guid id, CancellationToken cancellationToken)
    {
        if (!Ready)
            return Unavailable();

        ViewData["AutomationSection"] = "metrics";
        var history = await _presenter!.GetScenarioHistoryAsync(id, cancellationToken);
        if (history == null)
            return NotFound();

        return View(history);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (!Ready)
            return Unavailable();

        ViewData["AutomationSection"] = "metrics";
        var detail = await _presenter!.GetDetailAsync(id, cancellationToken);
        if (detail == null)
            return NotFound();

        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Compare(Guid a, Guid b, CancellationToken cancellationToken)
    {
        if (!Ready)
            return Unavailable();

        ViewData["AutomationSection"] = "metrics";
        var compare = await _presenter!.GetCompareAsync(a, b, cancellationToken);
        if (compare == null)
            return NotFound();

        return View(compare);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> LiveUtilization(CancellationToken cancellationToken)
    {
        if (_liveUtilization is null)
            return Json(new { reachable = false });

        var snapshot = await _liveUtilization.GetAsync(cancellationToken);
        return Json(snapshot);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!Ready)
            return Unavailable();

        var deleted = await _presenter!.DeleteSnapshotAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();

        return RedirectToAction(nameof(Index));
    }

    private ContentResult Unavailable() => new()
    {
        StatusCode = StatusCodes.Status503ServiceUnavailable,
        Content = _engine.Message ?? AutomationRunReader.NotConfiguredMessage,
        ContentType = "text/plain; charset=utf-8"
    };
}
